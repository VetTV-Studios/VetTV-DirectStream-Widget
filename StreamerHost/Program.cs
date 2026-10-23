using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
ChildProcessJob ffmpegJob = null;
TaskCompletionSource<bool> ffmpegExited = null;
AppServiceConnection connection = serviceConnection;
string logPath = null;

async Task StopFfmpegAsync()
{
    Process process = ffmpegProcess;
    ChildProcessJob job = ffmpegJob;
    TaskCompletionSource<bool> exitSignal = ffmpegExited;
    ffmpegProcess = null;
    ffmpegJob = null;
    ffmpegExited = null;

    if (process == null)
    {
        job?.Dispose();
        return;
    }

    try
    {
        if (!process.HasExited)
        {
            process.Kill(true);
            if (!process.WaitForExit(5000))
            {
                throw new TimeoutException("FFmpeg did not exit after the stop request.");
            }
        }
        if (exitSignal != null &&
            await Task.WhenAny(exitSignal.Task, Task.Delay(TimeSpan.FromSeconds(5))) != exitSignal.Task)
        {
            throw new TimeoutException("FFmpeg exited, but its shutdown was not confirmed.");
        }
    }
    finally
    {
        job?.Dispose();
        process.Dispose();
    }
}

void WriteLocalLog(string text)
{
    string line = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + text;
    Debug.WriteLine(line);
    try
    {
        if (logPath == null)
        {
            logPath = Path.Combine(
                Windows.Storage.ApplicationData.Current.LocalFolder.Path,
                "StreamerHost.log");
        }
        File.AppendAllText(logPath, line + Environment.NewLine);
    }
    catch (Exception ex)
    {
        Debug.WriteLine("Writing StreamerHost.log failed: " + ex.Message);
    }
}

async Task SendToWidget(ValueSet message)
{
    try
    {
        await connection.SendMessageAsync(message);
    }
    catch (Exception ex)
    {
        WriteLocalLog("Sending a host notification failed: " + ex.Message);
    }
}

async Task LogAsync(string text)
{
    WriteLocalLog(text);
    await SendToWidget(new ValueSet { ["Log"] = text });
}

bool IsUsefulDiagnostic(string text)
{
    if (text.Contains("passing through packet", StringComparison.OrdinalIgnoreCase) ||
        Regex.IsMatch(text, @"\bframe=\s*\d+", RegexOptions.IgnoreCase))
    {
        return false;
    }

    return Regex.IsMatch(
        text,
        @"\b(error|failed|warning|denied|refused|rejected|invalid|timeout|handshake|publish|rtmp)\b",
        RegexOptions.IgnoreCase) ||
        text.Contains("Starting connection attempt", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("Successfully connected", StringComparison.OrdinalIgnoreCase) ||
        text.StartsWith("Input #", StringComparison.OrdinalIgnoreCase) ||
        text.StartsWith("Output #", StringComparison.OrdinalIgnoreCase) ||
        text.StartsWith("Stream mapping:", StringComparison.OrdinalIgnoreCase) ||
        Regex.IsMatch(text, @"^\s*Stream #\d+:\d+: (Video|Audio):", RegexOptions.IgnoreCase);
}

List<string> ListEncoders(string ffmpeg)
{
    var output = RunAndCapture(ffmpeg, "-hide_banner -encoders");
    return new[] { "h264_nvenc", "h264_qsv", "h264_amf", "libx264" }
        .Where(e => output.Contains(e))
        .ToList();
}

async Task<List<string>> ListAudioDevicesAsync(string ffmpeg)
{
    string output;
    try
    {
        output = RunAndCapture(ffmpeg, "-hide_banner -list_devices true -f dshow -i dummy");
    }
    catch (Exception ex)
    {
        await LogAsync("Audio-device discovery failed; continuing without audio: " + ex.Message);
        return new List<string>();
    }

    var devices = new List<string>();
    foreach (string line in output.Split('\n'))
    {
        var match = Regex.Match(
            line,
            "\"(?<name>[^\"]+)\"\\s*\\(audio\\)",
            RegexOptions.IgnoreCase);
        if (match.Success)
        {
            devices.Add(match.Groups["name"].Value);
        }
    }
    return devices;
}

string RunAndCapture(string exe, string arguments)
{
    var psi = new ProcessStartInfo(exe, arguments)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    using var proc = Process.Start(psi);
    if (proc == null)
    {
        throw new InvalidOperationException("Could not start ffmpeg to inspect its capabilities.");
    }

    Task<string> stdout = proc.StandardOutput.ReadToEndAsync();
    Task<string> stderr = proc.StandardError.ReadToEndAsync();
    if (!proc.WaitForExit(30000))
    {
        proc.Kill(true);
        throw new TimeoutException("Timed out while querying ffmpeg capabilities.");
    }
    Task.WaitAll(stdout, stderr);
    return stdout.Result + Environment.NewLine + stderr.Result;
}

async Task<ValueSet> HandleStart(ValueSet message)
{
    WriteLocalLog("Received Start command.");
    try
    {
        return await HandleStartCore(message);
    }
    catch (Exception ex)
    {
        string key = message.TryGetValue("StreamKey", out object keyValue) ? keyValue as string ?? string.Empty : string.Empty;
        string url = message.TryGetValue("RtmpUrl", out object urlValue) ? urlValue as string ?? string.Empty : string.Empty;
        string error = ex.Message;
        if (!string.IsNullOrEmpty(key))
        {
            error = error.Replace(key, "[redacted]", StringComparison.Ordinal);
        }
        if (!string.IsNullOrEmpty(url))
        {
            error = error.Replace(url, "[redacted RTMP URL]", StringComparison.OrdinalIgnoreCase);
        }
        await LogAsync("Starting the stream failed: " + error);
        return new ValueSet { ["Status"] = "Error", ["Error"] = error };
    }
}

async Task<ValueSet> HandleStartCore(ValueSet message)
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
    var availableAudio = await ListAudioDevicesAsync(ffmpeg);
    if (GetBool("DesktopAudio") && availableAudio.Contains(
            FfmpegArgs.DesktopAudioDevice,
            StringComparer.OrdinalIgnoreCase))
    {
        audioDevices.Add(FfmpegArgs.DesktopAudioDevice);
    }
    else if (GetBool("DesktopAudio"))
    {
        await LogAsync(
            "Desktop/game audio is enabled, but FFmpeg cannot see the virtual-audio-capturer device. " +
            "Streaming without desktop audio; install a compatible virtual audio capture device.");
    }
    if (GetBool("MicAudio"))
    {
        string mic = availableAudio.FirstOrDefault(d =>
            !string.Equals(d, FfmpegArgs.DesktopAudioDevice, StringComparison.OrdinalIgnoreCase));
        if (mic != null)
        {
            audioDevices.Add(mic);
            await LogAsync("Using microphone input: " + mic);
        }
        else
        {
            await LogAsync("Mic requested but no microphone device was found by ffmpeg; streaming without mic.");
        }
    }

    string url = GetString("RtmpUrl").Trim();
    string key = GetString("StreamKey").Trim();
    string target = BuildTargetUrl(url, key);

    var args = FfmpegArgs.Build(quality, encoder, audioDevices, target);
    await LogAsync("Starting ffmpeg (" + encoder + ", " + quality.Label + "); waiting for RTMP output to open.");

    try
    {
        await StopFfmpegAsync();
        var outputReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processExited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var job = new ChildProcessJob();
        var psi = new ProcessStartInfo(ffmpeg, string.Join(" ", args.Select(QuoteArg)))
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.ErrorDataReceived += async (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                string diagnostic = e.Data.Replace(target, "[redacted RTMP target]", StringComparison.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(key))
                {
                    diagnostic = diagnostic.Replace(key, "[redacted]", StringComparison.Ordinal);
                }
                if (diagnostic.Contains("Output #0, flv,", StringComparison.OrdinalIgnoreCase))
                {
                    outputReady.TrySetResult(true);
                }
                if (IsUsefulDiagnostic(diagnostic))
                {
                    await LogAsync(diagnostic);
                }
            }
        };
        process.Exited += (_, _) =>
        {
            try
            {
                WriteLocalLog("ffmpeg exited with code " + process.ExitCode + ".");
            }
            catch (InvalidOperationException)
            {
                WriteLocalLog("ffmpeg exited.");
            }
            outputReady.TrySetResult(false);
            processExited.TrySetResult(true);
            _ = SendToWidget(new ValueSet { ["Status"] = "Idle" });
        };
        bool processStarted = false;
        try
        {
            processStarted = process.Start();
            if (!processStarted)
            {
                throw new InvalidOperationException("FFmpeg process did not start.");
            }
            job.Assign(process);
            ffmpegProcess = process;
            ffmpegJob = job;
            ffmpegExited = processExited;
            WriteLocalLog("ffmpeg process started with PID " + process.Id + ".");
            process.BeginErrorReadLine();
        }
        catch
        {
            if (processStarted)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                    }
                }
                catch (Exception cleanupException)
                {
                    WriteLocalLog("Cleaning up FFmpeg after startup failed: " + cleanupException.Message);
                }
            }
            job.Dispose();
            process.Dispose();
            if (ReferenceEquals(ffmpegProcess, process))
            {
                ffmpegProcess = null;
                ffmpegJob = null;
                ffmpegExited = null;
            }
            throw;
        }

        Task completed = await Task.WhenAny(outputReady.Task, Task.Delay(TimeSpan.FromSeconds(20)));
        if (completed != outputReady.Task)
        {
            await StopFfmpegAsync();
            const string timeout = "FFmpeg did not open the RTMP output within 20 seconds. Check the log for connection errors and verify the ingest URL.";
            await LogAsync(timeout);
            return new ValueSet { ["Status"] = "Error", ["Error"] = timeout };
        }

        if (!await outputReady.Task)
        {
            const string exited = "FFmpeg exited before opening the RTMP output. Check the log for details.";
            await LogAsync(exited);
            return new ValueSet { ["Status"] = "Error", ["Error"] = exited };
        }

        await LogAsync("RTMP output opened; FFmpeg is sending encoded data. The streaming platform must still confirm the broadcast.");
        return new ValueSet { ["Status"] = "Started" };
    }
    catch (Exception ex)
    {
        WriteLocalLog("Starting ffmpeg process failed: " + ex);
        return new ValueSet { ["Status"] = "Error", ["Error"] = ex.Message };
    }
}

string BuildTargetUrl(string url, string key)
{
    if (string.IsNullOrEmpty(key))
    {
        return url;
    }

    foreach (string placeholder in new[] { "{stream_key}", "{key}" })
    {
        if (url.IndexOf(placeholder, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return Regex.Replace(
                url,
                Regex.Escape(placeholder),
                _ => key,
                RegexOptions.IgnoreCase);
        }
    }

    return url.TrimEnd('/') + "/" + key;
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
            try
            {
                await StopFfmpegAsync();
                WriteLocalLog("Stop command completed; FFmpeg publisher terminated.");
                await args.Request.SendResponseAsync(new ValueSet { ["Status"] = "Stopped" });
                await SendToWidget(new ValueSet { ["Status"] = "Idle" });
            }
            catch (Exception ex)
            {
                WriteLocalLog("Stopping FFmpeg failed: " + ex.Message);
                await args.Request.SendResponseAsync(new ValueSet
                {
                    ["Status"] = "Error",
                    ["Error"] = ex.Message
                });
            }
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

WriteLocalLog("StreamerHost starting. App service name: DirectStreamService.");
var openStatus = await serviceConnection.OpenAsync();
WriteLocalLog("App service OpenAsync status: " + openStatus + ".");
if (openStatus != AppServiceConnectionStatus.Success)
{
    WriteLocalLog("Could not connect to the widget app service; exiting.");
    return;
}

// Announce this process as the host side of the relay.
var registration = await serviceConnection.SendMessageAsync(new ValueSet
{
    ["Command"] = "Register",
    ["Role"] = "Host"
});
WriteLocalLog("Host registration response: " + registration.Status + ".");

appServiceExit.Wait();
WriteLocalLog("Widget app service closed; stopping StreamerHost.");
try
{
    await StopFfmpegAsync();
}
catch (Exception ex)
{
    WriteLocalLog("Stopping ffmpeg after app-service close failed: " + ex.Message);
}
