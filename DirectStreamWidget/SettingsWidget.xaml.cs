using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace DirectStreamWidget
{
    public sealed partial class SettingsWidget : Page
    {
        public SettingsWidget() { InitializeComponent(); Load(); }
        void Load() {
            var s = ApplicationData.Current.LocalSettings;
            if(s.Values["DefaultRtmp"] is string v) DefaultRtmpBox.Text = v;
        }
        void OnSave(object sender, RoutedEventArgs e) {
            ApplicationData.Current.LocalSettings.Values["DefaultRtmp"] = DefaultRtmpBox.Text;
        }
    }
}
