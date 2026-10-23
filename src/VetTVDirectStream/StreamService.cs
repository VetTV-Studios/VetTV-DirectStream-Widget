using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace VetTVDirectStream;

public sealed class StreamService : IDisposable
{
    Process? _ff;
    AudioMixer? _audio;
    public event Action<string>? Log;
    public event Action<string>? Stats;      // e.g. "bitrate=6000kbits/s fps=60"
    public event Action? Exited;
    public bool IsRunning => _ff is { HasExited: false };

    public static string? FindFfmpeg()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");
        if (File.Exists(local)) return local;
        local = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(local)) return local;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            try { var f = Path.Combine(dir.Trim(), "ffmpeg.exe"); if (File.Exists(f)) return f; } catch { }
        }
        return null;
    }

    /// <summary>Returns the first encoder that actually works on this PC.</summary>
    public static async Task<string> DetectEncoderAsync(string ffmpeg)
    {
        foreach (var enc in new[] { "h264_nvenc", "h264_amf", "h264_qsv", "libx264" })
        {
            if (enc == "libx264") return enc;
            var psi = new ProcessStartInfo(ffmpeg, $"-hide_banner -loglevel error -f lavfi -i color=black:s=1280x720 -frames:v 5 -c:v {enc} -f null -")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            try
            {
                using var p = Process.Start(psi)!;
                var done = await Task.WhenAny(p.WaitForExitAsync(), Task.Delay(8000));
                if (!p.HasExited) { p.Kill(true); continue; }
                if (p.ExitCode == 0) return enc;
            }
            catch { }
        }
        return "libx264";
    }

    public static (int w, int h) Size(int res) => res switch { 0 => (1280, 720), 2 => (2560, 1440), _ => (1920, 1080) };

    public static int AutoBitrate(int res, int fps60) => (res, fps60) switch
    {
        (0, 0) => 3500, (0, _) => 4500,
        (1, 0) => 6000, (1, _) => 8000,
        (_, 0) => 9000, _ => 12000
    };

    public static string BuildArgs(AppSettings s, string encoder, string url)
    {
        var (w, h) = Size(s.Resolution);
        int fps = s.Fps == 1 ? 60 : 30;
        int br = s.BitrateKbps > 0 ? s.BitrateKbps : AutoBitrate(s.Resolution, s.Fps);
        string enc = encoder switch
        {
            "h264_nvenc" => $"-c:v h264_nvenc -preset p5 -tune ll -rc cbr -b:v {br}k -maxrate {br}k -bufsize {br * 2}k -bf 2",
            "h264_amf" => $"-c:v h264_amf -quality balanced -rc cbr -b:v {br}k -maxrate {br}k -bufsize {br * 2}k",
            "h264_qsv" => $"-c:v h264_qsv -preset medium -b:v {br}k -maxrate {br}k -bufsize {br * 2}k",
            _ => $"-c:v libx264 -preset veryfast -b:v {br}k -maxrate {br}k -bufsize {br * 2}k"
        };
        // gdigrab captures the whole virtual desktop starting at the primary monitor; scale to target.
        return $"-hide_banner -loglevel info -stats -y " +
               $"-f gdigrab -framerate {fps} -draw_mouse 1 -i desktop " +
               $"-f s16le -ar 48000 -ac 2 -i pipe:0 " +
               $"-map 0:v -map 1:a -vf \"scale={w}:{h}:flags=bicubic,format=yuv420p\" " +
               $"{enc} -g {fps * 2} -r {fps} -c:a aac -b:a 160k -ar 48000 -ac 2 " +
               $"-f flv \"{url}\"";
    }

    public void Start(AppSettings s, string ffmpegPath, string encoder)
    {
        if (IsRunning) return;
        string url = s.ServerUrl.TrimEnd('/') + "/" + s.StreamKey;
        var psi = new ProcessStartInfo(ffmpegPath, BuildArgs(s, encoder, url))
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardError = true
        };
        _ff = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var rx = new Regex(@"fps=\s*(\S+).*?bitrate=\s*(\S+)");
        _ff.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            var line = e.Data.Replace(s.StreamKey, "****"); // never log the key
            var m = rx.Match(line);
            if (m.Success) Stats?.Invoke($"{m.Groups[1].Value} fps  {m.Groups[2].Value}");
            else Log?.Invoke(line);
        };
        _ff.Exited += (_, _) => { _audio?.Dispose(); _audio = null; Exited?.Invoke(); };
        _ff.Start();
        _ff.BeginErrorReadLine();

        _audio = new AudioMixer(s.MicDevice)
        {
            GameOn = s.GameOn, MicOn = s.MicOn,
            GameVolume = (float)s.GameVolume, MicVolume = (float)s.MicVolume
        };
        _audio.Start(_ff.StandardInput.BaseStream);
    }

    public AudioMixer? Audio => _audio;

    public void Stop()
    {
        try { _ff?.StandardInput.Close(); } catch { }
        try { if (_ff != null && !_ff.WaitForExit(2500)) _ff.Kill(true); } catch { }
        _audio?.Dispose(); _audio = null;
    }

    public void Dispose() => Stop();
}
