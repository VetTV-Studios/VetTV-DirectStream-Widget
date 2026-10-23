using System.Collections.Generic;
using Windows.Storage;

namespace DirectStreamWidget
{
    /// <summary>
    /// RTMP ingest presets for the platforms the widget advertises.
    /// The stream key is appended to <see cref="IngestUrl"/> by the host;
    /// it is never stored here.
    /// </summary>
    public sealed class PlatformPreset
    {
        public string Name { get; }
        public string IngestUrl { get; }
        /// <summary>True when the platform expects the stream key appended to the URL path.</summary>
        public bool KeyAppendedToUrl { get; }

        public PlatformPreset(string name, string ingestUrl, bool keyAppendedToUrl = true)
        {
            Name = name;
            IngestUrl = ingestUrl;
            KeyAppendedToUrl = keyAppendedToUrl;
        }

        public override string ToString() => Name;
    }

    public static class PlatformPresets
    {
        public static readonly PlatformPreset VetTV =
            new PlatformPreset("VetTV", "rtmp://ingest.vettv.com/live");

        public static readonly PlatformPreset Twitch =
            new PlatformPreset("Twitch", "rtmp://live.twitch.tv/app");

        public static readonly PlatformPreset YouTube =
            new PlatformPreset("YouTube", "rtmp://a.rtmp.youtube.com/live2");

        public static readonly PlatformPreset Facebook =
            new PlatformPreset("Facebook", "rtmps://live-api-s.facebook.com:443/rtmp");

        // Kick also issues a per-channel ingest URL + stream key (Creator
        // Dashboard > Settings > Stream), so the URL is left blank on purpose
        // for the same reason as Rumble below.
        public static readonly PlatformPreset Kick =
            new PlatformPreset("Kick (paste your Kick ingest URL)", "", false);

        // Rumble gives each channel its own ingest URL + key pair from the
        // Rumble Studio / live dashboard, so there is no single shared URL.
        // The preset leaves the URL blank on purpose: the widget falls back to
        // the URL the user pasted in the RTMP field.
        public static readonly PlatformPreset Rumble =
            new PlatformPreset("Rumble (paste your Rumble ingest URL)", "", false);

        public static readonly PlatformPreset Custom =
            new PlatformPreset("Custom RTMP", "", false);

        public static IReadOnlyList<PlatformPreset> All { get; } = new List<PlatformPreset>
        {
            VetTV, Twitch, YouTube, Facebook, Kick, Rumble, Custom
        };
    }

    public static class ChatOverlayState
    {
        private const string PlatformSetting = "ChatPlatform";
        private static readonly ApplicationDataContainer Settings =
            ApplicationData.Current.LocalSettings;

        public static event System.Action<string> SelectedPlatformChanged;

        public static string SelectedPlatform =>
            Settings.Values[PlatformSetting] as string ?? PlatformPresets.VetTV.Name;

        public static void SetSelectedPlatform(string platform)
        {
            if (string.IsNullOrWhiteSpace(platform) ||
                string.Equals(SelectedPlatform, platform, System.StringComparison.Ordinal))
            {
                return;
            }

            Settings.Values[PlatformSetting] = platform;
            SelectedPlatformChanged?.Invoke(platform);
        }
    }
}
