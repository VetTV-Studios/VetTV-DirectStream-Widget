using System.Collections.Generic;

namespace DirectStreamWidget
{
    public class StreamPlatform
    {
        public string Name { get; set; }
        public string RtmpUrl { get; set; }
        public string ChatPopoutUrl { get; set; }
        public string HelpText { get; set; }
        public string Icon { get; set; }
        public bool IsVetTV { get; set; } = false;
    }

    public static class PlatformPresets
    {
        public static Dictionary<string, StreamPlatform> All = new()
        {
            ["VetTV"] = new StreamPlatform {
                Name = "VetTV",
                RtmpUrl = "rtmp://ingest.vettv.com/live",
                ChatPopoutUrl = "https://www.vettv.com/live-chat/{channel}",
                HelpText = "VetTV Direct Ingest - Use your VetTV Creator Stream Key. Default for VetTV Studios.",
                Icon = "🎖️",
                IsVetTV = true
            },
            ["Twitch"] = new StreamPlatform {
                Name = "Twitch",
                RtmpUrl = "rtmp://live.twitch.tv/app",
                ChatPopoutUrl = "https://www.twitch.tv/popout/{channel}/chat?popout=",
                HelpText = "Twitch: dashboard.twitch.tv -> Settings -> Stream Key",
                Icon = "📺"
            },
            ["YouTube"] = new StreamPlatform {
                Name = "YouTube",
                RtmpUrl = "rtmp://a.rtmp.youtube.com/live2",
                ChatPopoutUrl = "https://www.youtube.com/live_chat?v={videoId}&is_popout=1",
                HelpText = "YouTube Studio -> Go Live -> Stream Key",
                Icon = "▶️"
            },
            ["Facebook"] = new StreamPlatform {
                Name = "Facebook",
                RtmpUrl = "rtmps://live-api-s.facebook.com:443/rtmp/",
                ChatPopoutUrl = "https://www.facebook.com/{page}/live/",
                HelpText = "Facebook Live Producer -> Stream Key",
                Icon = "👍"
            },
            ["Kick"] = new StreamPlatform {
                Name = "Kick",
                RtmpUrl = "rtmp://fa723fc1b171.global-contribute.live-video.net/app/",
                ChatPopoutUrl = "https://kick.com/popout/{channel}/chat",
                HelpText = "Kick Creator Dashboard -> Settings -> Stream Key",
                Icon = "🟢"
            },
            ["Rumble"] = new StreamPlatform {
                Name = "Rumble",
                RtmpUrl = "rtmp://4d37a1b1b5a8.global-contribute.live-video.net/app/",
                ChatPopoutUrl = "https://rumble.com/chat/popup/{videoId}",
                HelpText = "Rumble -> Live Stream -> RTMP Info",
                Icon = "🔶"
            },
            ["Custom"] = new StreamPlatform {
                Name = "Custom RTMP",
                RtmpUrl = "",
                ChatPopoutUrl = "",
                HelpText = "Enter your own RTMP server",
                Icon = "⚙️"
            }
        };

        // Change this if your VetTV ingest is different
        public const string VetTVDefaultIngest = "rtmp://ingest.vettv.com/live";
    }
}
