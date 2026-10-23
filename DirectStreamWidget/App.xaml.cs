using System;
using System.Threading.Tasks;
using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace DirectStreamWidget
{
    /// <summary>
    /// Widget app entry point.
    ///
    /// Responsibilities beyond the original scaffold:
    ///  - OnBackgroundActivated hosts the "DirectStreamService" app service
    ///    declared in the manifest. The full-trust StreamerHost process
    ///    connects to it as a client; without this override nothing ever
    ///    received the host's connection (diagnosis item 9).
    ///  - EnsureHostLaunched starts StreamerHost via FullTrustProcessLauncher
    ///    the first time the widget is activated.
    /// </summary>
    public sealed partial class App : Application
    {
        private XboxGameBarWidget _mainWidget;
        private XboxGameBarWidget _settingsWidget;
        // The app service is a relay between two clients: the widget UI
        // (Role=Widget, StreamingService) and the full-trust host
        // (Role=Host, StreamerHost). Commands flow widget -> host;
        // Log/Status messages flow host -> widget.
        private AppServiceConnection _hostConnection;
        private AppServiceConnection _widgetConnection;
        private BackgroundTaskDeferral _appServiceDeferral;
        private bool _hostLaunchAttempted;

        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (!(Window.Current.Content is Frame rootFrame))
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(Widget1), e.Arguments);
                }
                Window.Current.Activate();
            }
        }

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            if (args.Kind == ActivationKind.Protocol &&
                args is IProtocolActivatedEventArgs protocolArgs &&
                protocolArgs.Uri.Scheme == "ms-gamebarwidget" &&
                args is XboxGameBarWidgetActivatedEventArgs widgetArgs)
            {
                if (widgetArgs.IsLaunchActivation)
                {
                    var rootFrame = new Frame();
                    rootFrame.NavigationFailed += OnNavigationFailed;
                    Window.Current.Content = rootFrame;

                    if (widgetArgs.AppExtensionId == "com.vettv.directstream")
                    {
                        await EnsureHostLaunchedAsync();
                        _mainWidget = new XboxGameBarWidget(
                            widgetArgs,
                            Window.Current.CoreWindow,
                            rootFrame);
                        rootFrame.Navigate(typeof(Widget1));
                    }
                    else if (widgetArgs.AppExtensionId == "DirectStreamSettings")
                    {
                        _settingsWidget = new XboxGameBarWidget(
                            widgetArgs,
                            Window.Current.CoreWindow,
                            rootFrame);
                        rootFrame.Navigate(typeof(SettingsWidget));
                    }

                    Window.Current.Activate();
                    return;
                }

                return;
            }

            base.OnActivated(args);
        }

        /// <summary>
        /// Hosts the app service and relays between its two clients.
        /// Each client announces itself with { Command = "Register", Role = "Host" | "Widget" }.
        /// </summary>
        protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            base.OnBackgroundActivated(args);

            if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails appService)
            {
                _appServiceDeferral = args.TaskInstance.GetDeferral();
                args.TaskInstance.Canceled += OnAppServiceCanceled;
                var connection = appService.AppServiceConnection;
                connection.RequestReceived += OnAppServiceRequestReceived;
                connection.ServiceClosed += OnAppServiceClosed;
            }
        }

        private async void OnAppServiceRequestReceived(
            AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var deferral = args.GetDeferral();
            try
            {
                var message = args.Request.Message;
                string command = message.TryGetValue("Command", out object c) ? c as string : null;

                if (command == "Register")
                {
                    string role = message.TryGetValue("Role", out object r) ? r as string : null;
                    if (role == "Host") _hostConnection = sender;
                    else if (role == "Widget") _widgetConnection = sender;
                    await args.Request.SendResponseAsync(
                        new Windows.Foundation.Collections.ValueSet { ["Status"] = "Registered" });
                    return;
                }

                bool fromHost = sender == _hostConnection;

                if (!fromHost && (command == "Start" || command == "Stop" || command == "Ping"))
                {
                    // Widget -> host command. Forward it and return the host's reply.
                    if (_hostConnection == null)
                    {
                        await args.Request.SendResponseAsync(
                            new Windows.Foundation.Collections.ValueSet
                            {
                                ["Status"] = "Error",
                                ["Error"] = "Streaming host is not connected yet. Reopen the widget so StreamerHost can launch."
                            });
                        return;
                    }

                    var hostResponse = await _hostConnection.SendMessageAsync(message);
                    await args.Request.SendResponseAsync(hostResponse.Message);
                    return;
                }

                if (fromHost && (message.ContainsKey("Log") || message.ContainsKey("Status")))
                {
                    // Host -> widget notification. Relay it; no response needed.
                    if (_widgetConnection != null)
                    {
                        await _widgetConnection.SendMessageAsync(message);
                    }
                    return;
                }
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void OnAppServiceCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            _appServiceDeferral?.Complete();
            _appServiceDeferral = null;
            _hostConnection?.Dispose();
            _widgetConnection?.Dispose();
            _hostConnection = null;
            _widgetConnection = null;
        }

        private void OnAppServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            if (sender == _hostConnection) _hostConnection = null;
            if (sender == _widgetConnection) _widgetConnection = null;
        }

        private async Task EnsureHostLaunchedAsync()
        {
            if (_hostLaunchAttempted)
            {
                return;
            }
            _hostLaunchAttempted = true;

            try
            {
                // Executable path matches the windows.fullTrustProcess
                // extension in Package.appxmanifest.
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
            }
            catch (Exception)
            {
                // Host failed to launch (e.g. package built without the host).
                // StreamingService will surface the real connection error to
                // the UI when the user presses Start; do not fake success.
                _hostLaunchAttempted = false;
            }
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
