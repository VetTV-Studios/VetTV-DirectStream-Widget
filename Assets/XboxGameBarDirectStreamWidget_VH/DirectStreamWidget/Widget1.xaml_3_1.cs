using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Microsoft.Gaming.XboxGameBar;

namespace DirectStreamWidget
{
    public sealed partial class Widget1 : Page
    {
        private XboxGameBarWidget widget;
        private StreamingService streamer = new StreamingService();
        private bool isStreaming = false;
        private DateTime streamStart;
        private DispatcherTimer statsTimer;
        private bool keyVisible = false;
        private TextBox visibleKeyBox;

        public Widget1() { 
            InitializeComponent(); 
            LoadSettings(); 
            InitTimer();
            visibleKeyBox = new TextBox { PlaceholderText = "Stream Key", FontSize = 11, Visibility = Visibility.Collapsed };
        }

        protected override void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            widget = e.Parameter as XboxGameBarWidget;
            base.OnNavigatedTo(e);
        }

        private void InitTimer()
        {
            statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            statsTimer.Tick += (s, ev) => {
                if(isStreaming) {
                    var elapsed = DateTime.Now - streamStart;
                    StatsText.Text = $"{elapsed:hh\:mm\:ss} | LIVE | {streamer.CurrentBitrate} | {streamer.CurrentEncoder}";
                }
            };
        }

        private void LoadSettings()
        {
            var local = ApplicationData.Current.LocalSettings;
            if(local.Values["RtmpUrl"] is string url) RtmpUrlBox.Text = url;
            if(local.Values["StreamKey"] is string key) StreamKeyBox.Password = key;
            if(local.Values["GameVol"] is double gv) GameVolSlider.Value = gv;
            if(local.Values["MicVol"] is double mv) MicVolSlider.Value = mv;
            if(local.Values["Platform"] is string pf) {
                foreach(ComboBoxItem item in PlatformBox.Items) if((string)item.Tag == pf) PlatformBox.SelectedItem = item;
            }
        }

        private void SaveSettings()
        {
            var local = ApplicationData.Current.LocalSettings;
            local.Values["RtmpUrl"] = RtmpUrlBox.Text;
            local.Values["StreamKey"] = StreamKeyBox.Password;
            local.Values["GameVol"] = GameVolSlider.Value;
            local.Values["MicVol"] = MicVolSlider.Value;
            if(PlatformBox.SelectedItem is ComboBoxItem cb) local.Values["Platform"] = (string)cb.Tag;
        }

        private void OnPlatformChanged(object sender, SelectionChangedEventArgs e)
        {
            if(PlatformBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if(PlatformPresets.All.TryGetValue(tag, out var preset))
                {
                    if(tag != "Custom") RtmpUrlBox.Text = preset.RtmpUrl;
                    PlatformHelp.Text = preset.HelpText;
                }
            }
        }

        private void OnGameVolChanged(object sender, RangeBaseValueChangedEventArgs e) => GameVolText.Text = $"{e.NewValue:0}%";
        private void OnMicVolChanged(object sender, RangeBaseValueChangedEventArgs e) => MicVolText.Text = $"{e.NewValue:0}%";

        private void OnToggleKey(object sender, RoutedEventArgs e)
        {
            keyVisible = !keyVisible;
            // Simple toggle for demo - in production swap PasswordBox/TextBox
            if(keyVisible) StreamKeyBox.PasswordRevealMode = PasswordRevealMode.Visible;
            else StreamKeyBox.PasswordRevealMode = PasswordRevealMode.Hidden;
        }

        private async void OnStreamButtonClick(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            if(!isStreaming)
            {
                if(string.IsNullOrWhiteSpace(RtmpUrlBox.Text) || string.IsNullOrWhiteSpace(StreamKeyBox.Password))
                {
                    LogBox.Text += "\n[ERROR] RTMP URL and Stream Key required.";
                    return;
                }

                LogBox.Text += $"\n[INFO] Starting {((ComboBoxItem)PlatformBox.SelectedItem)?.Tag} stream at {((ComboBoxItem)QualityBox.SelectedItem)?.Content}...";
                LogBox.Text += $"\n[INFO] Encoder: {((ComboBoxItem)EncoderBox.SelectedItem)?.Content} | Game {GameVolSlider.Value:0}% Mic {MicVolSlider.Value:0}%";
                StatusText.Text = "Connecting...";
                StatusLight.Fill = new SolidColorBrush(Colors.Yellow);
                StreamButtonText.Text = "STOP STREAMING";
                StreamButton.Background = new SolidColorBrush(Color.FromArgb(255, 80, 80, 80));

                double gameVol = GameVolSlider.Value / 100.0;
                double micVol = MuteMicCheck.IsChecked == true ? 0 : MicVolSlider.Value / 100.0;

                bool success = await streamer.StartAsync(
                    RtmpUrlBox.Text, StreamKeyBox.Password, QualityBox.SelectedIndex, EncoderBox.SelectedIndex,
                    DesktopAudioCheck.IsChecked==true, MicAudioCheck.IsChecked==true, gameVol, micVol, RtxFeaturesCheck.IsChecked==true,
                    (log) => { _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { LogBox.Text += "\n" + log; }); });

                if(success)
                {
                    isStreaming = true;
                    streamStart = DateTime.Now;
                    statsTimer.Start();
                    StatusText.Text = "LIVE";
                    StatusLight.Fill = new SolidColorBrush(Colors.Red);
                    EncoderStatus.Text = $"LIVE: {streamer.CurrentEncoder}";
                }
                else
                {
                    StatusText.Text = "Failed";
                    StatusLight.Fill = new SolidColorBrush(Colors.Gray);
                    StreamButtonText.Text = "START STREAMING";
                }
            }
            else
            {
                await streamer.StopAsync();
                isStreaming = false;
                statsTimer.Stop();
                StatusText.Text = "Idle";
                StatusLight.Fill = new SolidColorBrush(Colors.Gray);
                StreamButtonText.Text = "START STREAMING";
                StreamButton.Background = new SolidColorBrush(Color.FromArgb(255, 255, 26, 26));
                StatsText.Text = "Stopped";
                LogBox.Text += "\n[INFO] Stream stopped.";
            }
        }

        private void OnChatClick(object sender, RoutedEventArgs e)
        {
            var platformTag = (PlatformBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "Twitch";
            var channel = ChannelBox.Text?.Trim();
            if(string.IsNullOrWhiteSpace(channel)) {
                LogBox.Text += "\n[CHAT] Enter Channel / Video ID to open chat overlay";
                return;
            }
            // Open chat overlay widget
            if(widget != null) {
                // Store for chat overlay to read
                ApplicationData.Current.LocalSettings.Values["ChatPlatform"] = platformTag;
                ApplicationData.Current.LocalSettings.Values["ChatChannel"] = channel;
                LogBox.Text += $"\n[CHAT] Opening {platformTag} chat for {channel}...";
                // In real widget, you'd navigate or launch second widget
            }
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e) => widget?.ActivateSettingsWidget();
    }
}
