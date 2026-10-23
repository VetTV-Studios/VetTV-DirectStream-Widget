using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace DirectStreamWidget
{
    /// <summary>
    /// Settings page. Every control here is now actually read and written:
    /// the scaffold saved only DefaultRtmp (which nothing consumed), never
    /// read AutoReconnectCheck / SaveKeyCheck / FpsBox, and labeled plaintext
    /// key storage "(encrypted)". The key now lives in the PasswordVault when
    /// SaveKey is on (see StreamingService), and Widget1 consumes DefaultRtmp.
    /// Keys used by Widget1: "DefaultRtmp" (string), "SaveKey" (bool),
    /// "AutoReconnect" (bool), "PreferredFps" (int, 60 or 30).
    /// </summary>
    public sealed partial class SettingsWidget : Page
    {
        private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;

        public SettingsWidget()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            DefaultRtmpBox.Text = _settings.Values["DefaultRtmp"] as string ?? string.Empty;
            AutoReconnectCheck.IsChecked = !(_settings.Values["AutoReconnect"] is bool ar) || ar;
            SaveKeyCheck.IsChecked = !(_settings.Values["SaveKey"] is bool sk) || sk;
            FpsBox.SelectedIndex = (_settings.Values["PreferredFps"] is int fps && fps == 30) ? 1 : 0;
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            _settings.Values["DefaultRtmp"] = DefaultRtmpBox.Text?.Trim() ?? string.Empty;
            _settings.Values["AutoReconnect"] = AutoReconnectCheck.IsChecked == true;
            bool saveKey = SaveKeyCheck.IsChecked == true;
            _settings.Values["SaveKey"] = saveKey;
            _settings.Values["PreferredFps"] = FpsBox.SelectedIndex == 1 ? 30 : 60;

            if (!saveKey)
            {
                StreamingService.ClearStreamKey();
            }

            SavedText.Text = "Saved.";
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (Frame != null)
            {
                Frame.Navigate(typeof(Widget1));
            }
        }
    }
}
