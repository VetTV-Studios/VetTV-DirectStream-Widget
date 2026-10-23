using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace DirectStreamWidget
{
    public sealed partial class ChatOverlay : Page
    {
        public ChatOverlay()
        {
            InitializeComponent();
            LoadChat();
        }

        void LoadChat()
        {
            var local = ApplicationData.Current.LocalSettings;
            string platform = local.Values["ChatPlatform"] as string ?? "Twitch";
            string channel = local.Values["ChatChannel"] as string ?? "";

            ChatPlatformText.Text = $"{platform} - {channel}";
            ChatTitle.Text = $"{platform.ToUpper()} CHAT";

            if(PlatformPresets.All.TryGetValue(platform, out var preset) && !string.IsNullOrWhiteSpace(channel))
            {
                string url = preset.ChatPopoutUrl
                    .Replace("{channel}", channel)
                    .Replace("{videoId}", channel)
                    .Replace("{page}", channel);

                try {
                    ChatWebView.Navigate(new System.Uri(url));
                } catch {
                    ChatFallback.Visibility = Windows.UI.Xaml.Visibility.Visible;
                }
            }
            else
            {
                ChatFallback.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
        }

        void OnSendChat(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // In production: Use Twitch IRC, YouTube Live Chat API, etc.
            // For widget, this would call your backend or use stored OAuth tokens
            ChatMessages.Children.Add(new TextBlock { Text = $"You: {ChatInput.Text}", Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White), FontSize = 11 });
            ChatInput.Text = "";
        }
    }
}
