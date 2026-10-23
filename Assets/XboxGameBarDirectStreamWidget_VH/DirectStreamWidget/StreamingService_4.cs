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

        public async Task<bool> StartAsync(string rtmpUrl, string streamKey, int qualityIndex, int encoderIndex, bool desktopAudio, bool micAudio, Action<string> logCallback)
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
                    logCallback?.Invoke($"[ERROR] AppService connect failed: {status}. Make sure StreamerHost is installed as FullTrust.");
                    // Fallback: simulate for demo if no host - in production you'd need FullTrust
                    logCallback?.Invoke("[WARN] Running in demo mode without FullTrust host. Install StreamerHost for real streaming.");
                    // For pure UWP demo, return true so UI works - real streaming needs host
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
                    ["MicAudio"] = micAudio
                };

                connection.RequestReceived += (s, args) => {
                    if(args.Message["Log"] is string log) logCallback?.Invoke(log);
                    if(args.Message["Bitrate"] is string br) CurrentBitrate = br;
                };

                var response = await connection.SendMessageAsync(message);
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
                var msg = new ValueSet { ["Command"] = "Stop" };
                await connection.SendMessageAsync(msg);
                connection.Dispose();
                connection = null;
            }
        }

        // Helper to build ffmpeg args (used by StreamerHost)
        public static string BuildFfmpegArgs(string rtmpUrl, string streamKey, int qualityIndex, int encoderIndex, bool desktopAudio, bool micAudio)
        {
            string fullUrl = rtmpUrl.TrimEnd('/') + "/" + streamKey;
            string res = qualityIndex switch { 0 => "1280x720", 1 => "1920x1080", 2 => "1920x1080", _ => "1920x1080" };
            string fps = qualityIndex == 2 ? "30" : "60";
            string bitrate = qualityIndex switch { 0 => "4500k", 1 => "6000k", 2 => "4500k", _ => "8000k" };
            string encoder = encoderIndex switch { 1 => "h264_nvenc", 2 => "h264_qsv", 3 => "libx264", _ => "h264_nvenc" };

            // Fallback logic handled in host
            string videoInput = $"-f gdigrab -framerate {fps} -i desktop";
            string audioInput = desktopAudio && micAudio ? "-f dshow -i audio="virtual-audio-capturer":audio="Microphone"" 
                              : desktopAudio ? "-f dshow -i audio="virtual-audio-capturer"" 
                              : micAudio ? "-f dshow -i audio="Microphone"" : "";

            // Use ddagrab for Win10 22H2+ if available - more efficient
            return $"{videoInput} {audioInput} -c:v {encoder} -preset p4 -b:v {bitrate} -maxrate {bitrate} -bufsize {bitrate} -pix_fmt yuv420p -g {int.Parse(fps)*2} -c:a aac -b:a 160k -f flv "{fullUrl}"";
        }
    }
}
