using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StreamerHost
{
    /// <summary>One quality choice from the widget's QualityBox.</summary>
    public sealed class QualityPreset
    {
        public string Label { get; }
        public int Width { get; }
        public int Height { get; }
        public int Fps { get; }
        public string VideoBitrate { get; }

        public QualityPreset(string label, int width, int height, int fps, string videoBitrate)
        {
            Label = label;
            Width = width;
            Height = height;
            Fps = fps;
            VideoBitrate = videoBitrate;
        }
    }

    public static class QualityPresets
    {
        // Keys must match the ComboBox item text in Widget1.xaml.
        public static readonly IReadOnlyDictionary<string, QualityPreset> ByLabel =
            new Dictionary<string, QualityPreset>(StringComparer.OrdinalIgnoreCase)
            {
                ["720p60 - 4500k"]  = new QualityPreset("720p60 - 4500k",  1280, 720,  60, "4500k"),
                ["720p30 - 3000k"]  = new QualityPreset("720p30 - 3000k",  1280, 720,  30, "3000k"),
                ["1080p60 - 6000k"] = new QualityPreset("1080p60 - 6000k", 1920, 1080, 60, "6000k"),
                ["1080p30 - 4500k"] = new QualityPreset("1080p30 - 4500k", 1920, 1080, 30, "4500k"),
                ["1440p60 - 9000k"] = new QualityPreset("1440p60 - 9000k", 2560, 1440, 60, "9000k"),
                ["1440p30 - 7000k"] = new QualityPreset("1440p30 - 7000k", 2560, 1440, 30, "7000k"),
                // Legacy label shipped in the scaffold's QualityBox.
                ["Source - 8000k"]  = new QualityPreset("Source - 8000k", 1920, 1080, 60, "8000k"),
            };

        public static QualityPreset Resolve(string label)
        {
            if (!string.IsNullOrWhiteSpace(label) && ByLabel.TryGetValue(label.Trim(), out QualityPreset preset))
            {
                return preset;
            }
            return ByLabel["1080p60 - 6000k"];
        }
    }

    /// <summary>
    /// The single ffmpeg argument builder for the whole product.
    /// (The scaffold had two divergent copies; this replaces both.)
    ///
    /// Fixes vs. the scaffold:
    ///  - Resolution/FPS from the chosen preset are actually applied
    ///    (a scale filter + output -r), including 1440p.
    ///  - Presets are per-encoder: NVENC uses p4, QSV uses medium, x264 uses
    ///    veryfast. The scaffold passed NVENC's p4 to everything in one copy
    ///    and x264's "fast" to everything in the other.
    ///  - Multiple dshow audio devices are separate -i inputs mixed with
    ///    amix; the scaffold's two-device syntax was invalid.
    ///  - No hardcoded microphone name; the caller passes device names
    ///    enumerated from ffmpeg itself (see Program.ListAudioDevices).
    ///  - "Auto" resolves against the encoders actually present in the
    ///    installed ffmpeg build instead of always assuming NVENC.
    /// </summary>
    public static class FfmpegArgs
    {
        public const string DesktopAudioDevice = "virtual-audio-capturer";

        public static string ResolveEncoder(string requested, IReadOnlyCollection<string> availableEncoders)
        {
            string wanted = (requested ?? "Auto").Trim();

            if (wanted.StartsWith("Auto", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[] { "h264_nvenc", "h264_qsv", "h264_amf", "libx264" })
                {
                    if (availableEncoders == null || availableEncoders.Contains(candidate))
                    {
                        return candidate;
                    }
                }
                return "libx264";
            }
            if (wanted.IndexOf("QSV", StringComparison.OrdinalIgnoreCase) >= 0) return "h264_qsv";
            if (wanted.IndexOf("NVENC", StringComparison.OrdinalIgnoreCase) >= 0) return "h264_nvenc";
            if (wanted.IndexOf("AMF", StringComparison.OrdinalIgnoreCase) >= 0) return "h264_amf";
            if (wanted.IndexOf("x264", StringComparison.OrdinalIgnoreCase) >= 0) return "libx264";
            return "h264_nvenc";
        }

        private static string PresetFor(string encoder)
        {
            switch (encoder)
            {
                case "h264_nvenc": return "p4";
                case "h264_qsv": return "medium";
                case "h264_amf": return "balanced";
                default: return "veryfast"; // libx264
            }
        }

        /// <summary>
        /// Builds the full ffmpeg argument list.
        /// audioDevices: dshow device names to capture (desktop capture device
        /// and/or microphone), already resolved by the caller. Empty = no audio.
        /// target: full RTMP URL including the stream key.
        /// </summary>
        public static List<string> Build(
            QualityPreset quality, string encoder, IReadOnlyList<string> audioDevices, string target)
        {
            var args = new List<string>
            {
                "-hide_banner",
                "-loglevel", "verbose",
                // Video input: Windows desktop capture.
                "-f", "gdigrab",
                "-framerate", quality.Fps.ToString(),
                "-i", "desktop",
            };

            if (audioDevices != null)
            {
                foreach (string device in audioDevices.Where(d => !string.IsNullOrWhiteSpace(d)))
                {
                    args.Add("-f");
                    args.Add("dshow");
                    args.Add("-i");
                    args.Add("audio=" + device);
                }
            }

            // Video encode. gdigrab captures at the monitor's native size, so
            // the scale filter is what actually delivers 720p/1080p/1440p.
            args.Add("-vf");
            args.Add($"scale={quality.Width}:{quality.Height}:flags=bicubic,fps={quality.Fps},format=yuv420p");
            args.Add("-c:v");
            args.Add(encoder);
            args.Add("-preset");
            args.Add(PresetFor(encoder));
            args.Add("-b:v");
            args.Add(quality.VideoBitrate);
            args.Add("-maxrate");
            args.Add(quality.VideoBitrate);
            args.Add("-bufsize");
            args.Add(DoubleBitrate(quality.VideoBitrate));
            args.Add("-r");
            args.Add(quality.Fps.ToString());
            args.Add("-g");
            args.Add((quality.Fps * 2).ToString()); // 2-second keyframe interval, as platforms require

            int audioCount = audioDevices?.Count(d => !string.IsNullOrWhiteSpace(d)) ?? 0;
            if (audioCount == 1)
            {
                args.Add("-map"); args.Add("0:v:0");
                args.Add("-map"); args.Add("1:a:0");
                args.Add("-c:a"); args.Add("aac");
                args.Add("-b:a"); args.Add("160k");
                args.Add("-ar"); args.Add("44100");
            }
            else if (audioCount >= 2)
            {
                // Mix every audio input (game + mic) into one AAC track.
                var inputs = string.Concat(Enumerable.Range(1, audioCount).Select(i => $"[{i}:a:0]"));
                args.Add("-filter_complex");
                args.Add($"{inputs}amix=inputs={audioCount}:duration=longest[aout]");
                args.Add("-map"); args.Add("0:v:0");
                args.Add("-map"); args.Add("[aout]");
                args.Add("-c:a"); args.Add("aac");
                args.Add("-b:a"); args.Add("160k");
                args.Add("-ar"); args.Add("44100");
            }
            else
            {
                args.Add("-an");
            }

            args.Add("-f");
            args.Add("flv");
            args.Add(target);
            return args;
        }

        private static string DoubleBitrate(string bitrate)
        {
            if (bitrate != null && bitrate.EndsWith("k") &&
                int.TryParse(bitrate.TrimEnd('k'), out int kbps))
            {
                return (kbps * 2) + "k";
            }
            return bitrate;
        }

        /// <summary>
        /// Finds ffmpeg: next to the host, in the current directory, in the
        /// classic C:\ffmpeg\bin install, and finally on PATH - the scaffold
        /// claimed to search PATH but never did.
        /// </summary>
        public static string FindFfmpeg()
        {
            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"),
                Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg.exe"),
                @"C:\ffmpeg\bin\ffmpeg.exe",
            };

            string pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string dir in pathVar.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    candidates.Add(Path.Combine(dir.Trim(), "ffmpeg.exe"));
                }
            }

            return candidates.FirstOrDefault(File.Exists);
        }
    }
}
