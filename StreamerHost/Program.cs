using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using StreamerHost;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

// StreamerHost - full-trust companion process for the Direct Stream widget.
//
// Launched by the widget via FullTrustProcessLauncher (see the
// windows.fullTrustProcess extension in Package.appxmanifest). It connects to
// the "DirectStreamService" app service hosted by the widget process, registers
// itself with Role=Host, and then waits for Start/Stop commands relayed from
// the widget UI. All ffmpeg work happens here, never in the widget process.
//
// Changes from the scaffold:
//  - Uses the single shared FfmpegArgs builder (resolution/1440p applied,
//    per-encoder presets, valid multi-device dshow audio, PATH search).
//  - No hardcoded "Microphone (Realtek(R) Audio)": audio devices are
//    enumerated from ffmpeg itself.
//  - "Auto" encoder resolves against the encoders the installed ffmpeg
//    actually has, instead of always assuming NVENC.
//  - Start failures are reported back with the real error instead of the
//    widget pretending the stream is live.

var serviceConnection = new AppServiceConnection
{
    AppServiceName = "DirectStreamService",
    PackageFamilyName = Windows.ApplicationModel.Package.Current.Id.FamilyName
};

var appServiceExit = new ManualResetEventSlim(false);
Process ffmpegProcess = null;
AppServiceConnection connection = serviceConnection;

async Task SendToWidget(ValueSet message)
{
    try { await connection.SendMessageAsync(message); }
    catch { /* widget closed; nothing to notify */ }
}

async Task LogAsync(string text) =>
    await SendToWidget(new ValueSet { ["Log"] = text });

List<string> ListEncoders(string ffmpeg)
{
    var output = RunAndCapture(ffmpeg, "-hide_banner -encoders");
    return new[] { "h264_nvenc", "h264_qsv", "h264_amf", "libx264" }
        .Where(e => output.Contains(e))
        .ToList();
}

List<string> ListAudioDevices(string ffmpeg)
{
    // ffmpeg prints the DirectShow device list to stderr and exits non-zero;
    // RunAndCapture merges stdout+stderr for exactly this case.
    var output = RunAndCapture(ffmpeg, "-hide_banner -list_devices true -f dshow -i dummy");
    var devices = new List<string>();
    bool inAudioSection = false;
    foreach (string line in output.Split('\n'))
    {
        if (line.Contains("DirectShow audio devices")) inAudioSection = true;
        else if (line.Contains("DirectShow video devices")) inAudioSection = false;
        if (!inAudioSection) continue;
        var match = Regex.Match(line, "\"(?<name>[^\"]+)\"");
        if (match.Success && !line.Contains("Alternative name"))
        {
            devices.Add(match.Groups["name"].Value);
        }
    }
    return devices;
}

string RunAndCapture(string exe, string arguments)
{
    try
    {
        var psi = new ProcessStartInfo(exe, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        var sb = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        proc.WaitForExit(15000);
        return sb.ToString();
    }
    catch
    {
        return string.Empty;
    }
}

async Task<ValueSet> HandleStart(ValueSet message)
{
    string ffmpeg = FfmpegArgs.FindFfmpeg();
    if (ffmpeg == null)
    {
        return new ValueSet
        {
            ["Status"] = "Error",
            ["Error"] = "ffmpeg.exe not found. Put ffmpeg.exe next to StreamerHost.exe or add ffmpeg to PATH (see Settings)."
        };
    }

    string GetString(string key) =>
        message.TryGetValue(key, out object v) ? v as string ?? string.Empty : string.Empty;
    bool GetBool(string key) =>
        message.TryGetValue(key, out object v) && v is bool b && b;

    var quality = QualityPresets.Resolve(GetString("Quality"));
    var encoders = ListEncoders(ffmpeg);
    string encoder = FfmpegArgs.ResolveEncoder(GetString("Encoder"), encoders);

    var audioDevices = new List<string>();
    var availableAudio = ListAudioDevices(ffmpeg);
    if (GetBool("DesktopAudio") && availableAudio.Contains(FfmpegArgs.DesktopAudioDevice))
    {
        audioDevices.Add(FfmpegArgs.DesktopAudioDevice);
    }
    if (GetBool("MicAudio"))
    {
        // First real audio device that is not the desktop capturer. The
        // scaffold hardcoded one machine's Realtek mic name; never again.
        string mic = availableAudio.FirstOrDefault(d => d != FfmpegArgs.DesktopAudioDevice);
        if (mic != null)
        {
            audioDevices.Add(mic);
        }
        else
        {
            await LogAsync("Mic requested but no microphone device was found by ffmpeg; streaming without mic.");
        }
    }

    string url = GetString("RtmpUrl").Trim();
    string key = GetString("StreamKey").Trim();
    string target = string.IsNullOrEmpty(key) ? url : url.TrimEnd('/') + "/" + key;

    var args = FfmpegArgs.Build(quality, encoder, audioDevices, target);
    await LogAsync("Starting ffmpeg (" + encoder + ", " + quality.Label + ") -> " + url);

    try
    {
        ffmpegProcess?.Kill();
        ffmpegProcess?.Dispose();
        var psi = new ProcessStartInfo(ffmpeg, string.Join(" ", args.Select(QuoteArg)))
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        ffmpegProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
        ffmpegProcess.ErrorDataReceived += async (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                await LogAsync(e.Data);
            }
        };
        ffmpegProcess.Exited += async (_, _) =>
        {
            await SendToWidget(new ValueSet { ["Status"] = "Idle" });
        };
        ffmpegProcess.Start();
        ffmpegProcess.BeginErrorReadLine();
        return new ValueSet { ["Status"] = "Started" };
    }
    catch (Exception ex)
    {
        return new ValueSet { ["Status"] = "Error", ["Error"] = ex.Message };
    }
}

string QuoteArg(string arg)
{
    if (arg.IndexOfAny(new[] { ' ', '"', '[', ']', ':' }) >= 0 && !arg.StartsWith("audio="))
    {
        return "\"" + arg.Replace("\"", "\\\"") + "\"";
    }
    if (arg.StartsWith("audio="))
    {
        return "\"" + arg.Replace("\"", "\\\"") + "\"";
    }
    return arg;
}

serviceConnection.RequestReceived += async (sender, args) =>
{
    var deferral = args.GetDeferral();
    try
    {
        string command = args.Request.Message.TryGetValue("Command", out object c) ? c as string : null;
        if (command == "Start")
        {
            var result = await HandleStart(args.Request.Message);
            await args.Request.SendResponseAsync(result);
        }
        else if (command == "Stop")
        {
            try { ffmpegProcess?.Kill(); } catch { }
            ffmpegProcess?.Dispose();
            ffmpegProcess = null;
            await args.Request.SendResponseAsync(new ValueSet { ["Status"] = "Stopped" });
            await SendToWidget(new ValueSet { ["Status"] = "Idle" });
        }
        else if (command == "Ping")
        {
            await args.Request.SendResponseAsync(new ValueSet { ["Status"] = "OK" });
        }
    }
    finally
    {
        deferral.Complete();
    }
};

serviceConnection.ServiceClosed += (_, _) => appServiceExit.Set();

var openStatus = await serviceConnection.OpenAsync();
if (openStatus != AppServiceConnectionStatus.Success)
{
    // Nowhere to report to: the widget surfaces its own connection error.
    return;
}

// Announce this process as the host side of the relay.
await serviceConnection.SendMessageAsync(new ValueSet
{
    ["Command"] = "Register",
    ["Role"] = "Host"
});

appServiceExit.Wait();
try { ffmpegProcess?.Kill(); } catch { }
