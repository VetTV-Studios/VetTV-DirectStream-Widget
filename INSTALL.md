# Installing VetTV Direct Stream Widget

Xbox Game Bar widget for direct RTMP streaming (Win+G). No OBS needed.

## Requirements
- Windows 10 (build 19041 / 20H1 or newer) or Windows 11, x64
- Xbox Game Bar (included with Windows; update it in the Microsoft Store)
- ffmpeg (see step 3 below)
- Optional: NVIDIA GPU + current driver for NVENC; otherwise the widget falls back to Intel QSV or x264 (CPU)

## Install from a GitHub Release
1. Download the `.msix` from the latest release on this repo.
2. **Trust the signing certificate once.** Release packages are signed with the VetTV Studios self-signed certificate (subject `CN=VetTV Studios`, matching the app manifest). Windows won't install a sideloaded package until that cert is trusted:
   - Right-click the downloaded `.msix` > **Properties** > **Digital Signatures** tab > select the VetTV Studios signature > **Details** > **View Certificate** > **Install Certificate** > **Local Machine** > place it in **Trusted People** > Finish.
3. **Install ffmpeg.** Run `scripts\Get-Ffmpeg.ps1` from this repo (installs to `C:\ffmpeg\bin`, which the streaming host searches), or install ffmpeg any other way and add it to PATH.
4. Double-click the `.msix` and click **Install**.
5. Press **Win+G**. Open the widget menu (top bar), find **Direct Stream**, and pin it.
6. Pick your platform, paste your stream key, choose quality/encoder, hit **START STREAMING**.

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
4. Open `XboxGameBarDirectStreamWidget.sln`, build Release | x64 (StreamerHost builds first; the widget package carries its output).
5. Deploy the package, then press Win+G. (Setting the widget as the VS startup project and pressing F5 only runs it as a loose app outside Game Bar - that was the scaffold's wrong instruction.)
