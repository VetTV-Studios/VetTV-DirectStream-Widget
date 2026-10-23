using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Microsoft.Gaming.XboxGameBar;

namespace DirectStreamWidget
{
    sealed partial class App : Application
    {
        private XboxGameBarWidget mainWidget = null;
        private XboxGameBarWidget settingsWidget = null;

        public App() { InitializeComponent(); }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Normal launch - not from Game Bar
            Window.Current.Activate();
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            if (args is XboxGameBarWidgetActivatedEventArgs widgetArgs)
            {
                var widget = new XboxGameBarWidget(
                    widgetArgs,
                    Window.Current.CoreWindow,
                    widgetArgs.IsPinned ? Window.Current.Content as Frame : new Frame()
                );

                if (widgetArgs.WidgetId == "DirectStreamMain")
                {
                    mainWidget = widget;
                    widget.Control.Navigate(typeof(Widget1), widget);
                }
                else if (widgetArgs.WidgetId == "DirectStreamSettings")
                {
                    settingsWidget = widget;
                    widget.Control.Navigate(typeof(SettingsWidget), widget);
                }

                Window.Current.Activate();
            }
        }
    }
}
