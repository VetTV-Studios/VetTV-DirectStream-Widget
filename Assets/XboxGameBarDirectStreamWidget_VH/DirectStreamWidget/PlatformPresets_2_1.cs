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
    }

    public static class PlatformPresets
    {
        public static Dictionary<string, StreamPlatform> All = new()
        {
            ["Twitch"] = new StreamPlatform {
                Name = "Twitch",
                RtmpUrl = "rtmp://live.twitch.tv/app",
                ChatPopoutUrl = "https://www.twitch.tv/popout/{channel}/chat?popout=",
                HelpText = "Use your Twitch stream key from dashboard.twitch.tv",
                Icon = "📺"
            },
            ["YouTube"] = new StreamPlatform {
                Name = "YouTube",
                RtmpUrl = "rtmp://a.rtmp.youtube.com/live2",
                ChatPopoutUrl = "https://www.youtube.com/live_chat?v={videoId}&is_popout=1",
                HelpText = "Get key from YouTube Studio -> Go Live",
                Icon = "▶️"
            },
            ["Facebook"] = new StreamPlatform {
                Name = "Facebook",
                RtmpUrl = "rtmps://live-api-s.facebook.com:443/rtmp/",
                ChatPopoutUrl = "https://www.facebook.com/{page}/live/",
                HelpText = "Facebook Live Producer -> Use Stream Key",
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
                HelpText = "Rumble -> Live Stream -> RTMP Info. Rumble uses same ingest as Kick (same infra).",
                Icon = "🔶"
            },
            ["Custom"] = new StreamPlatform {
                Name = "Custom RTMP",
                RtmpUrl = "",
                ChatPopoutUrl = "",
                HelpText = "Enter your own RTMP server (VetTV, Owncast, etc.)",
                Icon = "⚙️"
            }
        };
    }
}
