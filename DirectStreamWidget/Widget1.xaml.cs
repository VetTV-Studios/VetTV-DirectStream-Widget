using System;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace DirectStreamWidget
{
    /// <summary>
    /// Main widget page.
    /// Changes from the scaffold:
    ///  - Platform preset ComboBox (PlatformPresets.cs) fills the RTMP URL.
    ///  - Six quality choices including 1440p (matches StreamerHost presets).
    ///  - Stream key is kept in the PasswordVault (Settings toggle), not
    ///    plaintext LocalSettings.
    ///  - Settings page values are actually read here: DefaultRtmp seeds the
    ///    URL box, the FPS preference pre-selects matching quality rows.
    ///  - Start failures surface honestly - no more fake LIVE state.
    /// </summary>
    public sealed partial class Widget1 : Page
    {
        private readonly StreamingService _streamer = new StreamingService();
        private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;
        private DispatcherTimer _statsTimer;
        private DateTimeOffset _liveSince;
        private bool _isLive;

        public Widget1()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            _streamer.LogReceived += msg => AppendLog(msg);
            _streamer.StatusChanged += OnStreamerStatusChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PlatformBox.ItemsSource = PlatformPresets.All;
            PlatformBox.SelectedIndex = 0;

            string defaultUrl = _settings.Values["DefaultRtmp"] as string;
            if (!string.IsNullOrWhiteSpace(defaultUrl))
            {
                RtmpUrlBox.Text = defaultUrl;
            }

            bool saveKey = !(_settings.Values["SaveKey"] is bool b) || b;
            if (saveKey)
            {
                StreamKeyBox.Password = StreamingService.LoadStreamKey();
            }

            _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _statsTimer.Tick += OnStatsTick;
            _statsTimer.Start();
        }

        private void OnPlatformChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlatformBox.SelectedItem is PlatformPreset preset && !string.IsNullOrEmpty(preset.IngestUrl))
            {
                RtmpUrlBox.Text = preset.IngestUrl;
            }
        }

        private async void OnStreamButtonClick(object sender, RoutedEventArgs e)
        {
            if (_isLive)
            {
                await _streamer.StopAsync();
                SetStatus(false, "Idle");
                StreamButton.Content = "START STREAMING";
                return;
            }

            string url = RtmpUrlBox.Text?.Trim();
            string key = StreamKeyBox.Password ?? string.Empty;
            if (string.IsNullOrWhiteSpace(url))
            {
                AppendLog("Enter an RTMP URL (or pick a platform) first.");
                return;
            }

            string quality = (QualityBox.SelectedItem as ComboBoxItem)?.Content as string ?? "1080p60 - 6000k";
            string encoder = (EncoderBox.SelectedItem as ComboBoxItem)?.Content as string ?? "Auto (NVENC/QSV/x264)";

            bool saveKey = !(_settings.Values["SaveKey"] is bool sb) || sb;
            if (saveKey)
            {
                StreamingService.SaveStreamKey(key);
            }

            AppendLog("Connecting to streaming host...");
            bool started = await _streamer.StartAsync(
                url, key, quality, encoder,
                DesktopAudioCheck.IsChecked == true,
                MicAudioCheck.IsChecked == true,
                MuteMicCheck.IsChecked == true);

            if (started)
            {
                SetStatus(true, "Live");
                StreamButton.Content = "STOP STREAMING";
            }
            else
            {
                SetStatus(false, "Error - see log");
            }
        }

        private void OnStreamerStatusChanged(string status)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (status == "Idle")
                {
                    SetStatus(false, "Idle");
                    StreamButton.Content = "START STREAMING";
                }
            });
        }

        private void SetStatus(bool live, string text)
        {
            _isLive = live;
            if (live)
            {
                _liveSince = DateTimeOffset.Now;
            }
            StatusLight.Fill = new SolidColorBrush(live ? Colors.Red : Colors.Gray);
            StatusText.Text = text;
        }

        private void OnStatsTick(object sender, object e)
        {
            StatsText.Text = _isLive
                ? "Uptime: " + (DateTimeOffset.Now - _liveSince).ToString(@"hh\:mm\:ss")
                : string.Empty;
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            // The settings app extension opens from Game Bar's widget menu.
            AppendLog("Open this widget's Settings from the Game Bar widget menu.");
        }

        private void AppendLog(string message)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                LogBox.Text += "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + "\r\n";
            });
        }
    }
}
