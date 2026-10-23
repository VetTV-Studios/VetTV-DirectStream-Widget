using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace DirectStreamWidget
{
    public class StreamingService
    {
        private AppServiceConnection connection;
        public string CurrentBitrate { get; private set; } = "0 kbps";
        public string CurrentEncoder { get; private set; } = "Auto";

        public async Task<bool> StartAsync(string rtmpUrl, string streamKey, int qualityIndex, int encoderIndex, 
            bool desktopAudio, bool micAudio, double gameVol, double micVol, bool useRtxFeatures, Action<string> logCallback)
        {
            try
            {
                connection = new AppServiceConnection
                {
                    AppServiceName = "DirectStreamService",
                    PackageFamilyName = Windows.ApplicationModel.Package.Current.Id.FamilyName
                };
                var status = await connection.OpenAsync();
                if(status != AppServiceConnectionStatus.Success)
                {
                    logCallback?.Invoke($"[WARN] AppService {status}. Running demo mode - install StreamerHost for real RTX NVENC streaming.");
                    CurrentEncoder = encoderIndex == 0 ? "NVENC Auto" : encoderIndex == 1 ? "NVENC RTX" : encoderIndex == 2 ? "NVENC HEVC" : encoderIndex == 3 ? "QSV" : "x264";
                    return true;
                }

                var message = new ValueSet
                {
                    ["Command"] = "Start",
                    ["RtmpUrl"] = rtmpUrl,
                    ["StreamKey"] = streamKey,
                    ["Quality"] = qualityIndex,
                    ["Encoder"] = encoderIndex,
                    ["DesktopAudio"] = desktopAudio,
                    ["MicAudio"] = micAudio,
                    ["GameVol"] = gameVol,
                    ["MicVol"] = micVol,
                    ["RtxFeatures"] = useRtxFeatures
                };

                connection.RequestReceived += (s, args) => {
                    if(args.Message["Log"] is string log) logCallback?.Invoke(log);
                    if(args.Message["Bitrate"] is string br) CurrentBitrate = br;
                };

                var response = await connection.SendMessageAsync(message);
                CurrentEncoder = GetEncoderName(encoderIndex);
                return response.Status == AppServiceResponseStatus.Success;
            }
            catch(Exception ex)
            {
                logCallback?.Invoke($"[EXCEPTION] {ex.Message}");
                return false;
            }
        }

        public async Task StopAsync()
        {
            if(connection != null)
            {
                await connection.SendMessageAsync(new ValueSet { ["Command"] = "Stop" });
                connection.Dispose();
                connection = null;
            }
        }

        static string GetEncoderName(int idx) => idx switch { 0 => "NVENC RTX (Auto)", 1 => "NVENC H.264 RTX", 2 => "NVENC HEVC RTX", 3 => "Intel QSV", 4 => "AMD AMF", _ => "x264 CPU" };

        public static (string resolution, string fps, string bitrate, string audioBitrate) GetQuality(int index)
        {
            // 0=720p30,1=720p60,2=1080p30,3=1080p60,4=1440p30,5=1440p60
            return index switch {
                0 => ("1280x720", "30", "3500k", "128k"),
                1 => ("1280x720", "60", "4500k", "160k"),
                2 => ("1920x1080", "30", "4500k", "160k"),
                3 => ("1920x1080", "60", "6000k", "160k"),
                4 => ("2560x1440", "30", "8000k", "192k"),
                5 => ("2560x1440", "60", "12000k", "192k"),
                _ => ("1920x1080", "60", "6000k", "160k")
            };
        }

        public static string BuildFfmpegArgs(string rtmpUrl, string streamKey, int qualityIndex, int encoderIndex, bool desktopAudio, bool micAudio, double gameVol, double micVol, bool rtxFeatures)
        {
            var (res, fps, bitrate, abitrate) = GetQuality(qualityIndex);
            string fullUrl = rtmpUrl.TrimEnd('/') + "/" + streamKey;
            
            string encoder = encoderIndex switch {
                0 => "h264_nvenc", // Auto RTX
                1 => "h264_nvenc", // RTX H264
                2 => "hevc_nvenc", // RTX HEVC
                3 => "h264_qsv",
                4 => "h264_amf",
                _ => "libx264"
            };

            string videoInput = $"-f gdigrab -framerate {fps} -video_size {res} -i desktop";

            // Audio with volume filters
            string audioInputs = "";
            string filterComplex = "";
            if(desktopAudio && micAudio) {
                audioInputs = "-f dshow -i audio="virtual-audio-capturer" -f dshow -i audio="Microphone"";
                filterComplex = $"-filter_complex "[0:a]volume={gameVol}[a1];[1:a]volume={micVol}[a2];[a1][a2]amix=inputs=2:duration=longest[aout]" -map 0:v -map "[aout]"";
            } else if(desktopAudio) {
                audioInputs = "-f dshow -i audio="virtual-audio-capturer"";
                filterComplex = $"-filter:a volume={gameVol} -map 0:v -map 1:a";
            } else if(micAudio) {
                audioInputs = "-f dshow -i audio="Microphone"";
                filterComplex = $"-filter:a volume={micVol} -map 0:v -map 1:a";
            } else {
                filterComplex = "-map 0:v";
            }

            string nvencTuning = "";
            if(encoder.Contains("nvenc")) {
                if(rtxFeatures) {
                    nvencTuning = "-preset p7 -tune ll -rc cbr_hq -multipass qres -spatial-aq 1 -temporal-aq 1 -rc-lookahead 32 -b_ref_mode 2";
                } else {
                    nvencTuning = "-preset p5 -tune ll -rc cbr -multipass qres";
                }
            } else if(encoder == "libx264") {
                nvencTuning = "-preset veryfast -tune zerolatency";
            }

            return $"{videoInput} {audioInputs} {filterComplex} -c:v {encoder} {nvencTuning} -s {res} -r {fps} -b:v {bitrate} -maxrate {bitrate} -bufsize {bitrate} -pix_fmt yuv420p -g {int.Parse(fps)*2} -c:a aac -b:a {abitrate} -ar 48000 -f flv "{fullUrl}"".Replace("  ", " ").Trim();
        }
    }
}
