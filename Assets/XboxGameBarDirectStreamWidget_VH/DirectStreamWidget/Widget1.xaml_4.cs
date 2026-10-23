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

        public Widget1() { InitializeComponent(); LoadSettings(); InitTimer(); }

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
                    StatsText.Text = $"{elapsed:hh\:mm\:ss} | LIVE | {streamer.CurrentBitrate}";
                }
            };
        }

        private void LoadSettings()
        {
            var local = ApplicationData.Current.LocalSettings;
            if(local.Values["RtmpUrl"] is string url) RtmpUrlBox.Text = url;
            if(local.Values["StreamKey"] is string key) StreamKeyBox.Password = key;
        }

        private void SaveSettings()
        {
            var local = ApplicationData.Current.LocalSettings;
            local.Values["RtmpUrl"] = RtmpUrlBox.Text;
            local.Values["StreamKey"] = StreamKeyBox.Password;
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

                var quality = QualityBox.SelectedIndex;
                var encoder = EncoderBox.SelectedIndex;
                var includeDesktop = DesktopAudioCheck.IsChecked == true;
                var includeMic = MicAudioCheck.IsChecked == true && MuteMicCheck.IsChecked != true;

                LogBox.Text += $"\n[INFO] Starting stream to {RtmpUrlBox.Text} ...";
                StatusText.Text = "Connecting...";
                StatusLight.Fill = new SolidColorBrush(Colors.Yellow);
                StreamButtonText.Text = "STOP STREAMING";
                StreamButton.Background = new SolidColorBrush(Color.FromArgb(255, 80, 80, 80));

                bool success = await streamer.StartAsync(RtmpUrlBox.Text, StreamKeyBox.Password, quality, encoder, includeDesktop, includeMic,
                    (log) => { Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { LogBox.Text += "\n" + log; }); });

                if(success)
                {
                    isStreaming = true;
                    streamStart = DateTime.Now;
                    statsTimer.Start();
                    StatusText.Text = "LIVE";
                    StatusLight.Fill = new SolidColorBrush(Colors.Red);
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

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            widget?.ActivateSettingsWidget();
        }
    }
}
