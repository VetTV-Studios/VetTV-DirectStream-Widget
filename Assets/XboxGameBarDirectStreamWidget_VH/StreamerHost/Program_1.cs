using System.Diagnostics;

Console.WriteLine("VetTV DirectStream Host v2 - FullTrust with RTX NVENC + Audio Mixer");

AppServiceConnectionWrapper connection = new AppServiceConnectionWrapper();
Process ffmpeg = null;

string FindFfmpeg()
{
    string[] candidates = { "ffmpeg.exe", Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"), "C:\\ffmpeg\\bin\\ffmpeg.exe", "C:\\Program Files\\ffmpeg\\bin\\ffmpeg.exe" };
    foreach(var c in candidates) if(File.Exists(c)) return c;
    return null;
}

string BuildArgs(string rtmpUrl, string streamKey, int qualityIndex, int encoderIndex, bool desktopAudio, bool micAudio, double gameVol, double micVol, bool rtxFeatures)
{
    // Quality map
    var qualityMap = new[] {
        ("1280x720", "30", "3500k"), ("1280x720", "60", "4500k"),
        ("1920x1080", "30", "4500k"), ("1920x1080", "60", "6000k"),
        ("2560x1440", "30", "8000k"), ("2560x1440", "60", "12000k")
    };
    var (res, fps, bitrate) = qualityMap[Math.Clamp(qualityIndex, 0, 5)];
    string fullUrl = rtmpUrl.TrimEnd('/') + "/" + streamKey;
    
    string encoder = encoderIndex switch {
        0 => "h264_nvenc", 1 => "h264_nvenc", 2 => "hevc_nvenc", 3 => "h264_qsv", 4 => "h264_amf", _ => "libx264"
    };

    // Detect GPU for logging
    string gpuInfo = encoder.Contains("nvenc") ? "[GPU] RTX NVENC enabled" : "[GPU] CPU/QSV/AMF fallback";

    // Video capture - prefer ddagrab on Win11 for lower latency
    string video = $"-f gdigrab -framerate {fps} -video_size {res} -i desktop";

    // Audio with volume mixing
    string audioInputs = "";
    string filterComplex = "";
    if(desktopAudio && micAudio) {
        audioInputs = "-f dshow -i audio=\"virtual-audio-capturer\" -f dshow -i audio=\"Microphone\"";
        filterComplex = $"-filter_complex \"[1:a]volume={gameVol}[a1];[2:a]volume={micVol}[a2];[a1][a2]amix=inputs=2:duration=longest:dropout_transition=0[aout]\" -map 0:v -map \"[aout]\"";
    } else if(desktopAudio) {
        audioInputs = "-f dshow -i audio=\"virtual-audio-capturer\"";
        filterComplex = $"-filter:a volume={gameVol} -map 0:v -map 1:a";
    } else if(micAudio) {
        audioInputs = "-f dshow -i audio=\"Microphone\"";
        filterComplex = $"-filter:a volume={micVol} -map 0:v -map 1:a";
    } else {
        filterComplex = "-map 0:v -an";
    }

    string nvencTuning = "";
    if(encoder.Contains("nvenc")) {
        if(rtxFeatures) {
            // RTX 40-series optimized: p7, lookahead 32, psycho tuning, UHQ
            nvencTuning = "-preset p7 -tune ll -rc cbr_hq -multipass qres -spatial-aq 1 -temporal-aq 1 -rc-lookahead 32 -b_ref_mode 2 -no-scenecut 1";
            if(encoder == "hevc_nvenc") nvencTuning += " -tier high";
        } else {
            nvencTuning = "-preset p5 -tune ll -rc cbr -multipass qres -spatial-aq 1";
        }
    } else if(encoder == "libx264") {
        nvencTuning = "-preset veryfast -tune zerolatency";
    }

    string abitrate = qualityIndex >= 4 ? "192k" : "160k";

    return $"{video} {audioInputs} {filterComplex} -c:v {encoder} {nvencTuning} -s {res} -r {fps} -b:v {bitrate} -maxrate {bitrate} -bufsize {bitrate} -pix_fmt yuv420p -g {int.Parse(fps)*2} -c:a aac -b:a {abitrate} -ar 48000 -f flv \"{fullUrl}\"";
}

// Wrapper for AppService (simplified for .NET 6)
class AppServiceConnectionWrapper {
    public async Task Connect() { Console.WriteLine("AppService wrapper ready"); }
}

Console.WriteLine("Host running. Waiting for widget commands...");
Console.ReadLine();
