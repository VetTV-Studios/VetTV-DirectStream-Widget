using System;
using System.Text.RegularExpressions;

namespace DirectStreamWidget
{
    internal static class ChatOverlayUriBuilder
    {
        public static string GetTargetHint(string platform)
        {
            if (platform == "Twitch")
            {
                return "Twitch channel name";
            }
            if (platform == "YouTube")
            {
                return "YouTube live video ID or watch URL";
            }
            if (platform == "Facebook")
            {
                return "Facebook Live video URL";
            }
            if (platform.StartsWith("Kick", StringComparison.OrdinalIgnoreCase))
            {
                return "Kick channel name";
            }

            return "Full HTTPS chat or live page URL";
        }

        public static Uri Build(string platform, string target)
        {
            string value = (target ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Enter the channel, video ID, or chat URL.", nameof(target));
            }

            if (platform == "Twitch")
            {
                if (!Regex.IsMatch(value, @"^[A-Za-z0-9_]{1,25}$"))
                {
                    throw new ArgumentException("Enter a Twitch channel name, not a stream key or URL.", nameof(target));
                }
                return new Uri("https://www.twitch.tv/popout/" + value + "/chat?popout=");
            }

            if (platform == "YouTube")
            {
                string videoId = GetYouTubeVideoId(value);
                return new Uri(
                    "https://www.youtube.com/live_chat?is_popout=1&v=" +
                    Uri.EscapeDataString(videoId));
            }

            if (platform.StartsWith("Kick", StringComparison.OrdinalIgnoreCase))
            {
                if (!Regex.IsMatch(value, @"^[A-Za-z0-9_]{3,25}$"))
                {
                    throw new ArgumentException("Enter a Kick channel name, not a stream key or URL.", nameof(target));
                }
                return new Uri("https://kick.com/" + Uri.EscapeDataString(value) + "/chatroom");
            }

            Uri customUri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out customUri) ||
                customUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException("Enter a full HTTPS live or chat page URL.", nameof(target));
            }

            if (platform == "Facebook" &&
                !IsHostOrSubdomain(customUri.Host, "facebook.com") &&
                !IsHostOrSubdomain(customUri.Host, "fb.watch"))
            {
                throw new ArgumentException("Enter a Facebook Live page URL.", nameof(target));
            }

            if (platform == "Rumble" &&
                !IsHostOrSubdomain(customUri.Host, "rumble.com"))
            {
                throw new ArgumentException("Enter a Rumble live or chat page URL.", nameof(target));
            }

            if (platform == "VetTV" &&
                !IsHostOrSubdomain(customUri.Host, "vettv.com"))
            {
                throw new ArgumentException("Enter a VetTV live or chat page URL.", nameof(target));
            }

            return customUri;
        }

        private static string GetYouTubeVideoId(string value)
        {
            if (Regex.IsMatch(value, @"^[A-Za-z0-9_-]{11}$"))
            {
                return value;
            }

            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) ||
                !(IsHostOrSubdomain(uri.Host, "youtube.com") ||
                  IsHostOrSubdomain(uri.Host, "youtu.be")))
            {
                throw new ArgumentException("Enter an 11-character YouTube video ID or a YouTube watch/live URL.", nameof(value));
            }

            string videoId = null;
            if (IsHostOrSubdomain(uri.Host, "youtu.be"))
            {
                videoId = uri.AbsolutePath.Trim('/');
            }
            else if (uri.AbsolutePath.Equals("/watch", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string part in uri.Query.TrimStart('?').Split('&'))
                {
                    string[] pair = part.Split(new[] { '=' }, 2);
                    if (pair.Length == 2 && pair[0] == "v")
                    {
                        videoId = Uri.UnescapeDataString(pair[1]);
                        break;
                    }
                }
            }
            else
            {
                string[] segments = uri.AbsolutePath.Split(
                    new[] { '/' },
                    StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2 &&
                    (segments[0] == "live" || segments[0] == "embed" || segments[0] == "shorts"))
                {
                    videoId = segments[1];
                }
            }

            if (string.IsNullOrEmpty(videoId) ||
                !Regex.IsMatch(videoId, @"^[A-Za-z0-9_-]{11}$"))
            {
                throw new ArgumentException("The YouTube URL does not contain a valid video ID.", nameof(value));
            }

            return videoId;
        }

        private static bool IsHostOrSubdomain(string host, string domain)
        {
            return host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
        }
    }
}
