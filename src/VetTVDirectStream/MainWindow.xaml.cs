using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace VetTVDirectStream;

public partial class MainWindow : Window
{
    static readonly (string Name, string Url)[] Platforms =
    {
        ("Twitch", "rtmp://live.twitch.tv/app"),
        ("YouTube", "rtmp://a.rtmp.youtube.com/live2"),
        ("Facebook", "rtmps://live-api-s.facebook.com:443/rtmp"),
        ("Kick (paste URL from dashboard)", ""),
        ("Rumble (paste URL from dashboard)", ""),
        ("VetTV ingest / Custom", ""),
    };

    readonly AppSettings _s = AppSettings.Load();
    readonly StreamService _svc = new();
    readonly DispatcherTimer _meter = new() { Interval = TimeSpan.FromMilliseconds(100) };
    string? _ffmpeg;
    string _encoder = "libx264";

    public MainWindow()
    {
        InitializeComponent();
        foreach (var p in Platforms) PlatformBox.Items.Add(p.Name);
        ResBox.ItemsSource = new[] { "720p", "1080p", "1440p" };
        FpsBox.ItemsSource = new[] { "30 fps", "60 fps" };
        EncBox.ItemsSource = new[] { "auto", "h264_nvenc", "h264_amf", "h264_qsv", "libx264" };
        var mics = AudioMixer.MicDevices();
        mics.Insert(0, ("", "Default microphone"));
        MicBox.ItemsSource = mics.Select(m => new MicItem(m.Id, m.Name)).ToList();
        MicBox.SelectedIndex = 0;

        int pi = Array.FindIndex(Platforms, p => p.Name == _s.Platform); if (pi < 0) pi = 0;
        PlatformBox.SelectedIndex = pi;
        if (!string.IsNullOrEmpty(_s.ServerUrl)) ServerBox.Text = _s.ServerUrl;
        KeyBox.Password = _s.StreamKey;
        ResBox.SelectedIndex = _s.Resolution; FpsBox.SelectedIndex = _s.Fps;
        BitrateBox.Text = _s.BitrateKbps.ToString();
        EncBox.SelectedItem = _s.Encoder; if (EncBox.SelectedIndex < 0) EncBox.SelectedIndex = 0;
        GameOn.IsChecked = _s.GameOn; MicOn.IsChecked = _s.MicOn;
        GameVol.Value = _s.GameVolume; MicVol.Value = _s.MicVolume;

        _svc.Log += l => Dispatcher.InvokeAsync(() => { LogBox.AppendText(l + "\n"); LogBox.ScrollToEnd(); });
        _svc.Stats += t => Dispatcher.InvokeAsync(() => StatsText.Text = t);
        _svc.Exited += () => Dispatcher.InvokeAsync(() => { SetLive(false); StatusText.Text = "Stream ended - check log"; });
        _meter.Tick += (_, _) =>
        {
            GameMeter.Value = _svc.Audio?.GameLevel ?? 0; MicMeter.Value = _svc.Audio?.MicLevel ?? 0;
        };
        _meter.Start();

        _ffmpeg = StreamService.FindFfmpeg();
        if (_ffmpeg == null)
        {
            StatusText.Text = "ffmpeg.exe not found - reinstall VetTV DirectStream";
            GoBtn.IsEnabled = false;
        }
    }

    record MicItem(string Id, string Name);

    void PlatformBox_Changed(object s, SelectionChangedEventArgs e)
    {
        if (PlatformBox.SelectedIndex < 0) return;
        var url = Platforms[PlatformBox.SelectedIndex].Url;
        if (url != "") ServerBox.Text = url;
    }

    void Mixer_Changed(object s, RoutedEventArgs e) => PushMixer();
    void Mixer_Slider(object s, RoutedPropertyChangedEventArgs<double> e) => PushMixer();
    void PushMixer()
    {
        if (!IsLoaded || _svc.Audio is not { } a) return;
        a.GameOn = GameOn.IsChecked == true; a.MicOn = MicOn.IsChecked == true;
        a.GameVolume = (float)GameVol.Value; a.MicVolume = (float)MicVol.Value;
    }

    void SetLive(bool live)
    {
        GoBtn.Content = live ? "Stop Streaming" : "Go Live";
        foreach (Control c in new Control[] { PlatformBox, ServerBox, KeyBox, ResBox, FpsBox, BitrateBox, EncBox, MicBox })
            c.IsEnabled = !live;
        if (!live) StatsText.Text = "";
    }

    async void GoBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_svc.IsRunning) { _svc.Stop(); SetLive(false); StatusText.Text = "Stopped"; return; }
        if (string.IsNullOrWhiteSpace(ServerBox.Text) || string.IsNullOrEmpty(KeyBox.Password))
        { StatusText.Text = "Enter the server URL and stream key first"; return; }

        GoBtn.IsEnabled = false; StatusText.Text = "Starting...";
        _s.Platform = Platforms[Math.Max(0, PlatformBox.SelectedIndex)].Name;
        _s.ServerUrl = ServerBox.Text.Trim(); _s.StreamKey = KeyBox.Password;
        _s.Resolution = ResBox.SelectedIndex; _s.Fps = FpsBox.SelectedIndex;
        _s.BitrateKbps = int.TryParse(BitrateBox.Text, out var b) ? Math.Max(0, b) : 0;
        _s.Encoder = (string)EncBox.SelectedItem;
        _s.MicDevice = (MicBox.SelectedItem as MicItem) is { Id: not "" } mi ? mi.Id : null;
        _s.GameOn = GameOn.IsChecked == true; _s.MicOn = MicOn.IsChecked == true;
        _s.GameVolume = GameVol.Value; _s.MicVolume = MicVol.Value;
        _s.Save();

        _encoder = _s.Encoder == "auto" ? await StreamService.DetectEncoderAsync(_ffmpeg!) : _s.Encoder;
        try
        {
            _svc.Start(_s, _ffmpeg!, _encoder);
            SetLive(true); StatusText.Text = $"LIVE  ({_encoder})";
        }
        catch (Exception ex) { StatusText.Text = "Could not start: " + ex.Message; }
        GoBtn.IsEnabled = true;
    }

    void Window_Closing(object? sender, CancelEventArgs e) { _svc.Dispose(); }
}
