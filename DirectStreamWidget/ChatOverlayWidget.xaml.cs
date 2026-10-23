using System;
using System.Linq;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace DirectStreamWidget
{
    public sealed partial class ChatOverlayWidget : Page
    {
        private const string TargetSettingPrefix = "ChatTarget:";
        private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;
        private Uri _chatUri;

        public ChatOverlayWidget()
        {
            InitializeComponent();
            PlatformBox.ItemsSource = PlatformPresets.All;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ChatOverlayState.SelectedPlatformChanged += OnSharedPlatformChanged;
            string selectedPlatform = ChatOverlayState.SelectedPlatform;
            PlatformPreset preset = PlatformPresets.All.FirstOrDefault(
                item => item.Name.Equals(selectedPlatform, StringComparison.OrdinalIgnoreCase));
            PlatformBox.SelectedItem = preset ?? PlatformPresets.VetTV;

            LoadSavedChat();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ChatOverlayState.SelectedPlatformChanged -= OnSharedPlatformChanged;
        }

        private void OnPlatformChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(PlatformBox.SelectedItem is PlatformPreset preset))
            {
                return;
            }

            ChatOverlayState.SetSelectedPlatform(preset.Name);
            TargetBox.Header = "Channel / live video";
            TargetBox.PlaceholderText = ChatOverlayUriBuilder.GetTargetHint(preset.Name);
            ChatTitle.Text = preset.Name.ToUpperInvariant() + " CHAT";
            TargetBox.Text = _settings.Values[GetTargetSetting(preset.Name)] as string ?? string.Empty;
            LoadSavedChat();
        }

        private void OnLoadChatClick(object sender, RoutedEventArgs e)
        {
            if (!(PlatformBox.SelectedItem is PlatformPreset preset))
            {
                StatusText.Text = "Choose a streaming platform first.";
                return;
            }

            try
            {
                _chatUri = ChatOverlayUriBuilder.Build(preset.Name, TargetBox.Text);
                ChatOverlayState.SetSelectedPlatform(preset.Name);
                _settings.Values[GetTargetSetting(preset.Name)] = TargetBox.Text.Trim();
                BrowserButton.Visibility = Visibility.Visible;
                StatusText.Text = "Loading " + preset.Name + " chat…";
                ChatView.Navigate(_chatUri);
            }
            catch (ArgumentException ex)
            {
                StatusText.Text = ex.Message;
            }
        }

        private async void OnOpenInBrowserClick(object sender, RoutedEventArgs e)
        {
            if (_chatUri == null)
            {
                return;
            }

            try
            {
                bool launched = await Launcher.LaunchUriAsync(_chatUri);
                StatusText.Text = launched
                    ? "Opened chat in your default browser."
                    : "Windows could not open the chat page in your browser.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Could not open chat in the browser: " + ex.Message;
            }
        }

        private void OnNavigationStarting(WebView sender, WebViewNavigationStartingEventArgs e)
        {
            if (e.Uri == null || e.Uri.Scheme != Uri.UriSchemeHttps)
            {
                e.Cancel = true;
                StatusText.Text = "Blocked a chat-page navigation that was not HTTPS.";
                return;
            }

            StatusText.Text = "Loading platform chat…";
        }

        private void OnNavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs e)
        {
            StatusText.Text = e.IsSuccess
                ? "Chat loaded. Sign in to the platform in the embedded page if needed."
                : "Chat page could not load in the embedded browser. Try Open in browser.";
        }

        private void OnNavigationFailed(object sender, WebViewNavigationFailedEventArgs e)
        {
            StatusText.Text =
                "Embedded chat failed (" + e.WebErrorStatus + "). Try Open in browser.";
        }

        private void LoadSavedChat()
        {
            if (!(PlatformBox.SelectedItem is PlatformPreset preset) ||
                string.IsNullOrWhiteSpace(TargetBox.Text))
            {
                return;
            }

            try
            {
                _chatUri = ChatOverlayUriBuilder.Build(preset.Name, TargetBox.Text);
                BrowserButton.Visibility = Visibility.Visible;
                ChatView.Navigate(_chatUri);
            }
            catch (ArgumentException)
            {
                _chatUri = null;
            }
        }

        private static string GetTargetSetting(string platform)
        {
            return TargetSettingPrefix + platform;
        }

        private void OnSharedPlatformChanged(string platform)
        {
            _ = Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                () =>
                {
                    PlatformPreset preset = PlatformPresets.All.FirstOrDefault(
                        item => item.Name.Equals(platform, StringComparison.OrdinalIgnoreCase));
                    if (preset != null && PlatformBox.SelectedItem != preset)
                    {
                        PlatformBox.SelectedItem = preset;
                    }
                });
        }
    }
}
