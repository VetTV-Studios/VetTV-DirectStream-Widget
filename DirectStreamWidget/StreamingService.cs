using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Security.Credentials;
using Windows.UI.Xaml;

namespace DirectStreamWidget
{
    /// <summary>
    /// Widget-side client for the "DirectStreamService" app service.
    ///
    /// Changes from the original scaffold:
    ///  - NO demo mode. The original returned true when the connection failed,
    ///    so the UI showed LIVE while nothing was streaming. Connection and
    ///    command failures now throw / return false with the real reason.
    ///  - NO ffmpeg argument building here. Args are built once, correctly, in
    ///    the host (StreamerHost/FfmpegArgs.cs). This class only sends the
    ///    user's choices.
    ///  - The stream key can be kept in the Windows PasswordVault instead of
    ///    plaintext LocalSettings (Settings > "Save stream key" toggle).
    /// </summary>
    public sealed class StreamingService
    {
        public const string ServiceName = "DirectStreamService";
        private const string VaultResource = "VetTV.DirectStreamWidget";

        private AppServiceConnection _connection;

        public event Action<string> LogReceived;
        public event Action<string> StatusChanged;

        public async Task<bool> ConnectAsync()
        {
            if (_connection != null)
            {
                return true;
            }

            _connection = new AppServiceConnection
            {
                AppServiceName = ServiceName,
                PackageFamilyName = Windows.ApplicationModel.Package.Current.Id.FamilyName
            };
            _connection.RequestReceived += OnRequestReceived;
            _connection.ServiceClosed += OnServiceClosed;

            AppServiceConnectionStatus status;
            try
            {
                status = await _connection.OpenAsync();
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke("Opening the streaming host connection failed: " + ex.Message);
                _connection.Dispose();
                _connection = null;
                return false;
            }

            if (status == AppServiceConnectionStatus.Success)
            {
                try
                {
                    AppServiceResponse registration = await _connection.SendMessageAsync(new ValueSet
                    {
                        ["Command"] = "Register",
                        ["Role"] = "Widget"
                    });
                    if (registration.Status != AppServiceResponseStatus.Success ||
                        registration.Message == null ||
                        !registration.Message.TryGetValue("Status", out object registered) ||
                        (registered as string) != "Registered")
                    {
                        LogReceived?.Invoke(
                            "The streaming service did not confirm widget registration (status = " +
                            registration.Status + ").");
                    }
                }
                catch (Exception ex)
                {
                    LogReceived?.Invoke("Registering the widget with the streaming service failed: " + ex.Message);
                }
            }
            if (status != AppServiceConnectionStatus.Success)
            {
                LogReceived?.Invoke(
                    "Could not reach the streaming host (AppServiceConnectionStatus = " + status + "). " +
                    "Make sure the package was installed with StreamerHost included and try reopening the widget.");
                _connection.Dispose();
                _connection = null;
                return false;
            }

            LogReceived?.Invoke("Connected to streaming host.");
            return true;
        }

        public async Task<bool> StartAsync(
            string rtmpUrl, string streamKey, string quality, string encoder,
            bool desktopAudio, bool micAudio, bool muteMic,
            double gameVolume = 1.0, double micVolume = 1.0)
        {
            if (!await ConnectAsync())
            {
                return false;
            }

            var message = new ValueSet
            {
                ["Command"] = "Start",
                ["RtmpUrl"] = rtmpUrl ?? string.Empty,
                ["StreamKey"] = streamKey ?? string.Empty,
                ["Quality"] = quality ?? string.Empty,
                ["Encoder"] = encoder ?? string.Empty,
                ["DesktopAudio"] = desktopAudio,
                ["MicAudio"] = micAudio && !muteMic,
                ["GameVolume"] = ClampVolume(gameVolume),
                ["MicVolume"] = ClampVolume(micVolume)
            };

            AppServiceResponse response;
            try
            {
                response = await _connection.SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke("Sending the Start command failed: " + ex.Message);
                StatusChanged?.Invoke("Error");
                return false;
            }

            if (response.Status != AppServiceResponseStatus.Success)
            {
                LogReceived?.Invoke("Host did not answer the Start command (status = " + response.Status + ").");
                StatusChanged?.Invoke("Error");
                return false;
            }

            string result = response.Message != null &&
                response.Message.TryGetValue("Status", out object s) ? s as string : null;
            if (result == "Started")
            {
                StatusChanged?.Invoke("Sending (platform unconfirmed)");
                return true;
            }

            string error = response.Message != null &&
                response.Message.TryGetValue("Error", out object e) ? e as string : "Host returned no error details.";
            LogReceived?.Invoke("Host refused to start: " + error);
            StatusChanged?.Invoke("Error");
            return false;
        }

        private static double ClampVolume(double volume)
        {
            if (double.IsNaN(volume) || volume < 0.0) return 0.0;
            return volume > 1.0 ? 1.0 : volume;
        }

        public async Task<bool> StopAsync()
        {
            if (await SendStopCommandAsync())
            {
                StatusChanged?.Invoke("Idle");
                return true;
            }

            ResetConnection();
            if (Application.Current is App app && await app.EnsureHostLaunchedAsync())
            {
                if (await ConnectAsync() && await SendStopCommandAsync())
                {
                    StatusChanged?.Invoke("Idle");
                    return true;
                }
            }

            LogReceived?.Invoke(
                "Could not confirm stream stop because the streaming host did not reconnect. " +
                "Check the dashboard and retry Stop from the widget.");
            return false;
        }

        private async Task<bool> SendStopCommandAsync()
        {
            if (_connection == null)
            {
                return false;
            }

            try
            {
                var message = new ValueSet { ["Command"] = "Stop" };
                Task<AppServiceResponse> request = _connection.SendMessageAsync(message).AsTask();
                Task completed = await Task.WhenAny(request, Task.Delay(TimeSpan.FromSeconds(8)));
                if (completed != request)
                {
                    LogReceived?.Invoke("The Stop request timed out before the host confirmed shutdown.");
                    return false;
                }

                AppServiceResponse response = await request;
                string result = response.Message != null &&
                    response.Message.TryGetValue("Status", out object status) ? status as string : null;
                if (response.Status != AppServiceResponseStatus.Success || result != "Stopped")
                {
                    string error = response.Message != null &&
                        response.Message.TryGetValue("Error", out object details) ?
                        details as string : "The streaming host did not confirm that FFmpeg stopped.";
                    LogReceived?.Invoke("Stop was not confirmed: " + error);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke("Stop request failed: " + ex.Message);
                return false;
            }
        }

        private void ResetConnection()
        {
            _connection?.Dispose();
            _connection = null;
        }

        private void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var deferral = args.GetDeferral();
            try
            {
                if (args.Request.Message.TryGetValue("Log", out object log) && log is string text)
                {
                    LogReceived?.Invoke(text);
                }
                if (args.Request.Message.TryGetValue("Status", out object st) && st is string status)
                {
                    StatusChanged?.Invoke(status);
                }
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void OnServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            StatusChanged?.Invoke("Idle");
            LogReceived?.Invoke("Streaming host connection closed.");
            ResetConnection();
        }

        // ---- Stream key storage (PasswordVault, not plaintext) ----

        public static void SaveStreamKey(string key)
        {
            var vault = new PasswordVault();
            ClearStreamKey();
            if (!string.IsNullOrEmpty(key))
            {
                vault.Add(new PasswordCredential(VaultResource, "StreamKey", key));
            }
        }

        public static string LoadStreamKey()
        {
            try
            {
                var vault = new PasswordVault();
                PasswordCredential cred = vault.Retrieve(VaultResource, "StreamKey");
                cred.RetrievePassword();
                return cred.Password;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static void ClearStreamKey()
        {
            try
            {
                var vault = new PasswordVault();
                PasswordCredential cred = vault.Retrieve(VaultResource, "StreamKey");
                vault.Remove(cred);
            }
            catch (Exception)
            {
                // Nothing stored.
            }
        }
    }
}
