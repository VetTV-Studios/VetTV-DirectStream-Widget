using System.Diagnostics;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

Console.WriteLine("VetTV DirectStream Host - FullTrust process");

AppServiceConnection connection = null;
Process ffmpeg = null;

async Task ConnectAsync()
{
    var appService = new AppServiceConnection
    {
        AppServiceName = "DirectStreamService",
        PackageFamilyName = Windows.ApplicationModel.Package.Current.Id.FamilyName
    };
    
    appService.RequestReceived += async (s, args) =>
    {
        var msg = args.GetDeferral();
        try
        {
            var cmd = args.Request.Message["Command"] as string;
            if(cmd == "Start")
            {
                string rtmp = args.Request.Message["RtmpUrl"] as string;
                string key = args.Request.Message["StreamKey"] as string;
                int quality = (int)args.Request.Message["Quality"];
                int encoder = (int)args.Request.Message["Encoder"];
                bool desktop = (bool)args.Request.Message["DesktopAudio"];
                bool mic = (bool)args.Request.Message["MicAudio"];

                string ffmpegArgs = BuildArgs(rtmp, key, quality, encoder, desktop, mic);
                
                // Locate ffmpeg
                string ffmpegPath = FindFfmpeg();
                if(ffmpegPath == null)
                {
                    await args.Request.SendResponseAsync(new ValueSet{ ["Error"] = "ffmpeg.exe not found. Place it next to this exe or in PATH" });
                    return;
                }

                ffmpeg = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = ffmpegArgs,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                ffmpeg.ErrorDataReceived += async (sender, e) =>
                {
                    if(e.Data != null) await s.SendMessageAsync(new ValueSet{ ["Log"] = e.Data });
                };

                ffmpeg.Start();
                ffmpeg.BeginErrorReadLine();

                await args.Request.SendResponseAsync(new ValueSet{ ["Status"] = "Started" });
            }
            else if(cmd == "Stop")
            {
                try { ffmpeg?.Kill(true); } catch {}
                ffmpeg = null;
                await args.Request.SendResponseAsync(new ValueSet{ ["Status"] = "Stopped" });
            }
        }
        finally { msg.Complete(); }
    };

    await appService.OpenAsync();
    Console.WriteLine("Connected to AppService");
}

string FindFfmpeg()
{
    string[] candidates = { "ffmpeg.exe", Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"), "C:\ffmpeg\bin\ffmpeg.exe" };
    foreach(var c in candidates) if(File.Exists(c)) return c;
    return null;
}

string BuildArgs(string rtmpUrl, string streamKey, int qualityIndex, int encoderIndex, bool desktopAudio, bool micAudio)
{
    // Same logic as UWP service but with fallback encoder detection
    string fullUrl = rtmpUrl.TrimEnd('/') + "/" + streamKey;
    string fps = qualityIndex == 2 ? "30" : "60";
    string bitrate = qualityIndex switch { 0 => "4500k", 1 => "6000k", 2 => "4500k", _ => "8000k" };
    string encoder = encoderIndex switch { 1 => "h264_nvenc", 2 => "h264_qsv", 3 => "libx264", _ => "h264_nvenc" };
    
    // Prefer ddagrab on Windows 11 for lower overhead
    string video = $"-f gdigrab -framerate {fps} -offset_x 0 -offset_y 0 -video_size 1920x1080 -i desktop";
    string audio = "";
    if(desktopAudio) audio += " -f dshow -i audio="virtual-audio-capturer"";
    if(micAudio) audio += " -f dshow -i audio="Microphone (Realtek(R) Audio)"";
    
    return $"-y {video} {audio} -c:v {encoder} -preset fast -b:v {bitrate} -maxrate {bitrate} -bufsize {bitrate} -pix_fmt yuv420p -g {int.Parse(fps)*2} -c:a aac -b:a 160k -ar 48000 -f flv "{fullUrl}"";
}

await ConnectAsync();
Console.ReadLine();
