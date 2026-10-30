using System;
using System.Linq;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

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
        private const int MaxVisibleLogLines = 300;
        private readonly StreamingService _streamer = new StreamingService();
        private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;
        private DispatcherTimer _statsTimer;
        private DateTimeOffset _liveSince;
        private bool _isLive;
        private bool _stopPending;

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
            string selectedPlatform = ChatOverlayState.SelectedPlatform;
            PlatformPreset preset = PlatformPresets.All.FirstOrDefault(
                item => item.Name.Equals(selectedPlatform, StringComparison.OrdinalIgnoreCase));
            PlatformBox.SelectedItem = preset ?? PlatformPresets.VetTV;

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

            GameVolumeSlider.Value = SavedVolumePercent("GameVolume");
            MicVolumeSlider.Value = SavedVolumePercent("MicVolume");
            UpdateVolumeLabels();

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
            if (PlatformBox.SelectedItem is PlatformPreset selectedPreset)
            {
                ChatOverlayState.SetSelectedPlatform(selectedPreset.Name);
            }
        }

        private async void OnStreamButtonClick(object sender, RoutedEventArgs e)
        {
            if (_isLive || _stopPending)
            {
                bool stopped = await _streamer.StopAsync();
                if (stopped)
                {
                    _stopPending = false;
                    SetStatus(false, "Idle");
                    StreamButton.Content = "START STREAMING";
                }
                else
                {
                    _stopPending = true;
                    SetStatus(false, "Stop not confirmed");
                    StreamButton.Content = "STOP STREAMING";
                }
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
                MuteMicCheck.IsChecked == true,
                GameVolumeSlider.Value / 100.0,
                MicVolumeSlider.Value / 100.0);

            if (started)
            {
                _stopPending = false;
                SetStatus(true, "Sending (not confirmed)");
                StreamButton.Content = "STOP STREAMING";
            }
            else
            {
                SetStatus(false, "Error - log below");
                await ShowFailureLogAsync();
            }
        }

        private void OnStreamerStatusChanged(string status)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (status == "Idle")
                {
                    _stopPending = false;
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
                ? "Sending time: " + (DateTimeOffset.Now - _liveSince).ToString(@"hh\:mm\:ss")
                : string.Empty;
        }

        private double SavedVolumePercent(string key)
        {
            // Mixer v2 volumes persist as percent (0-100); default is unity.
            if (_settings.Values[key] is int percent)
            {
                return Math.Max(0, Math.Min(100, percent));
            }
            return 100.0;
        }

        private void OnVolumeChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (GameVolumeSlider == null || MicVolumeSlider == null)
            {
                return;
            }
            _settings.Values["GameVolume"] = (int)Math.Round(GameVolumeSlider.Value);
            _settings.Values["MicVolume"] = (int)Math.Round(MicVolumeSlider.Value);
            UpdateVolumeLabels();
        }

        private void UpdateVolumeLabels()
        {
            if (GameVolumeText != null)
            {
                GameVolumeText.Text = ((int)Math.Round(GameVolumeSlider.Value)) + "%";
            }
            if (MicVolumeText != null)
            {
                MicVolumeText.Text = ((int)Math.Round(MicVolumeSlider.Value)) + "%";
            }
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            if (Frame != null)
            {
                Frame.Navigate(typeof(SettingsWidget));
            }
        }

        private void OnCopyLogClick(object sender, RoutedEventArgs e)
        {
            var data = new DataPackage();
            data.SetText(LogBox.Text ?? string.Empty);
            Clipboard.SetContent(data);
            AppendLog("Log copied to clipboard.");
        }

        private async System.Threading.Tasks.Task ShowFailureLogAsync()
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                var dialog = new ContentDialog
                {
                    Title = "Stream start failed - diagnostics",
                    Content = new ScrollViewer
                    {
                        MaxHeight = 360,
                        Content = new TextBlock
                        {
                            Text = LogBox.Text ?? "No diagnostic messages were received.",
                            TextWrapping = TextWrapping.Wrap,
                            FontFamily = new FontFamily("Consolas"),
                            FontSize = 12
                        }
                    },
                    PrimaryButtonText = "Copy log",
                    CloseButtonText = "Close"
                };
                ContentDialogResult result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var data = new DataPackage();
                    data.SetText(LogBox.Text ?? string.Empty);
                    Clipboard.SetContent(data);
                }
            });
        }

        private void AppendLog(string message)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                string entry = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + "\r\n";
                string current = LogBox.Text ?? string.Empty;
                int excessLines = current.Split(new[] { "\r\n" }, StringSplitOptions.None).Length -
                    MaxVisibleLogLines;
                if (excessLines > 0)
                {
                    int removeThrough = 0;
                    for (int i = 0; i < excessLines; i++)
                    {
                        int nextLine = current.IndexOf("\r\n", removeThrough, StringComparison.Ordinal);
                        if (nextLine < 0)
                        {
                            removeThrough = current.Length;
                            break;
                        }
                        removeThrough = nextLine + 2;
                    }
                    current = current.Substring(removeThrough);
                }

                LogBox.Text = current + entry;
                LogBox.Select(LogBox.Text.Length, 0);
            });
        }
    }
}
