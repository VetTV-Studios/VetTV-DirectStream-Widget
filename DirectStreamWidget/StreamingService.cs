using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Security.Credentials;

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

            AppServiceConnectionStatus status = await _connection.OpenAsync();
            if (status == AppServiceConnectionStatus.Success)
            {
                // Announce this side of the relay (see App.OnBackgroundActivated).
                try
                {
                    await _connection.SendMessageAsync(new ValueSet
                    {
                        ["Command"] = "Register",
                        ["Role"] = "Widget"
                    });
                }
                catch (Exception)
                {
                    // Registration is best-effort; command responses still flow.
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
            bool desktopAudio, bool micAudio, bool muteMic)
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
                ["MicAudio"] = micAudio && !muteMic
            };

            AppServiceResponse response = await _connection.SendMessageAsync(message);
            if (response.Status != AppServiceResponseStatus.Success)
            {
                LogReceived?.Invoke("Host did not answer the Start command (status = " + response.Status + ").");
                return false;
            }

            string result = response.Message.TryGetValue("Status", out object s) ? s as string : null;
            if (result == "Started")
            {
                StatusChanged?.Invoke("Live");
                return true;
            }

            string error = response.Message.TryGetValue("Error", out object e) ? e as string : "unknown error";
            LogReceived?.Invoke("Host refused to start: " + error);
            StatusChanged?.Invoke("Error");
            return false;
        }

        public async Task StopAsync()
        {
            if (_connection == null)
            {
                return;
            }

            try
            {
                var message = new ValueSet { ["Command"] = "Stop" };
                await _connection.SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke("Stop failed: " + ex.Message);
            }
            finally
            {
                StatusChanged?.Invoke("Idle");
            }
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
            _connection?.Dispose();
            _connection = null;
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
