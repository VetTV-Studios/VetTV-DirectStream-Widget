# Installing VetTV Direct Stream Widget

Xbox Game Bar widget for direct RTMP streaming (Win+G). No OBS needed.

## Requirements
- Windows 10 (build 19041 / 20H1 or newer) or Windows 11, x64
- Xbox Game Bar (included with Windows; update it in the Microsoft Store)
- ffmpeg (see step 3 below)
- Optional: NVIDIA GPU + current driver for NVENC; otherwise the widget falls back to Intel QSV or x264 (CPU)

## Install from a GitHub Release
1. Download the `.msix` and `VetTVStudios-Signing.cer` from the same release.
2. **Trust the signing certificate once.** Release packages are signed with the VetTV Studios self-signed certificate (subject `CN=VetTV Studios`, matching the app manifest). Windows won't install a sideloaded package until that certificate is trusted. From an elevated PowerShell opened in the repository, run `.\scripts\Install-Cert.ps1 -CertificatePath "C:\path\to\VetTVStudios-Signing.cer"`. Alternatively, right-click the `.cer` > **Install Certificate** > **Local Machine** > **Trusted People** > Finish. Only trust the certificate when it came from the official VetTV Studios release.
3. **Install ffmpeg.** Run `scripts\Get-Ffmpeg.ps1` from this repo (installs to `C:\ffmpeg\bin`, which the streaming host searches), or install ffmpeg any other way and add it to PATH.
4. Double-click the `.msix` and click **Install**.
5. Press **Win+G**. Open the widget menu (top bar), find **Direct Stream**, and pin it.
6. Pick your platform, paste your stream key, choose quality/encoder, hit **START STREAMING**. If a custom RTMP URL contains `{stream_key}` or `{key}`, the widget replaces that placeholder with the key; otherwise it appends the key to the URL path.
7. To show chat over a game, open the Game Bar widget menu and pin **Direct Stream Chat**. It follows the platform selected in Direct Stream. Enter the Twitch/Kick channel name, YouTube live video ID or watch URL, or a full HTTPS live/chat page URL for the other platforms, then select **LOAD CHAT**. YouTube may reject the older UWP embedded browser; use **Open in browser** in that case.

If starting a stream fails, the widget shows the reason in the **LOG** panel below the controls. Use **Copy log** to copy the diagnostics. “Sending (not confirmed)” means FFmpeg opened the RTMP output; the streaming platform must still confirm it received the broadcast. Detailed FFmpeg diagnostics include RTMP connection and publish-handshake messages; routine per-packet and frame-progress lines are excluded from the UI to keep the widget responsive, and the visible log is capped at 300 lines. Stream keys and RTMP targets are redacted. The Stop button waits for the host to confirm FFmpeg has exited, and FFmpeg is automatically terminated if its host exits. The cog opens the widget settings; the Game Bar widget menu's settings button is also supported.

The full-trust streaming host also writes startup and FFmpeg diagnostics to `%LOCALAPPDATA%\Packages\VetTVStudios.DirectStreamWidget_ma6trnq8c46ft\LocalState\StreamerHost.log`.

Microphone devices are discovered from FFmpeg's DirectShow device list. Desktop/game audio requires the DirectShow `virtual-audio-capturer` device; if it is not installed, the log reports that desktop audio is unavailable rather than silently claiming it is captured. If the streaming host disconnects, Stop attempts to relaunch it and reconnect before reporting failure.

Notes:
- Sideloading is enabled by default on supported Windows builds; you do **not** need Developer Mode just to install. Developer Mode is only needed for Visual Studio deployment while developing.
- If the widget doesn't appear in Game Bar, close Game Bar fully and press Win+G again - Game Bar discovers newly installed widgets on launch.

## Stream keys
- **Twitch / YouTube / Facebook:** the widget appends your key to the platform's ingest URL automatically.
- **Kick / Rumble:** these services give every channel its own ingest URL. Paste the full ingest URL from your Kick Creator Dashboard / Rumble Studio into the RTMP URL field, and your key as usual.
- **VetTV:** default ingest is `rtmp://ingest.vettv.com/live`.

Your stream key is stored in the Windows Credential Vault when "Save stream key" is on in Settings - never in plaintext.

## Uninstall
Settings > Apps > Direct Stream > Uninstall. Game Bar forgets the widget automatically.

## For developers (build it yourself)
1. Visual Studio with the **Universal Windows Platform development** workload, Windows SDK 10.0.26100, and the .NET 8 SDK.
2. Add the six PNGs listed in `DirectStreamWidget\Assets\ASSETS-REQUIRED.md`.
3. Create a signing cert whose subject matches the manifest Publisher (`CN=VetTV Studios`) - `scripts\New-SigningCert.ps1` - and pick it in the project's Packaging settings, or build unsigned for local deploy.
4. Run `dotnet publish StreamerHost\StreamerHost.csproj -c Release -r win-x64 --self-contained true`, then open `XboxGameBarDirectStreamWidget.sln` and build Release | x64. The self-contained StreamerHost executable is included in the widget package.
5. Deploy the package, then press Win+G. (Setting the widget as the VS startup project and pressing F5 only runs it as a loose app outside Game Bar - that was the scaffold's wrong instruction.)

To publish through GitHub Actions, configure the repository secrets `SIGNING_CERT_PFX_BASE64` (the base64-encoded PFX) and `SIGNING_CERT_PASSWORD`. The release workflow stops if either secret is missing so it cannot publish an unsigned MSIX. Never commit the PFX or its password.
