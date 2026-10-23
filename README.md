# VetTV Direct Stream Widget

Xbox Game Bar widget for direct RTMP streaming (Win+G). One-click Go Live from Game Bar - no OBS needed.

**Platforms:** Twitch, YouTube, Facebook, Kick, Rumble, and VetTV ingest built in as presets, plus any custom RTMP URL.
**Encoders:** NVIDIA NVENC, Intel QSV, AMD AMF, or x264 - with real auto-detection and fallback based on the ffmpeg build you have installed.
**Quality:** 720p / 1080p / 1440p at 30 or 60 fps, 2-second keyframe interval as the platforms require.
**Audio:** desktop/game audio and microphone mixed into one AAC track (via ffmpeg + DirectShow).
**Chat:** a separately pinnable Xbox Game Bar chat widget follows the selected platform and opens its live chat page.

## Install
See [INSTALL.md](INSTALL.md). Short version: download the `.msix` from Releases, trust the VetTV Studios signing certificate once, install ffmpeg (`scripts\Get-Ffmpeg.ps1`), install the package, then Win+G and pin **Direct Stream**.

## How it works
- The widget (UWP) is just the UI: platform/quality/encoder picks, stream key (kept in the Windows Credential Vault), status and log.
- A full-trust companion process, **StreamerHost**, ships in the same package and does the actual work: it runs ffmpeg (gdigrab desktop capture + DirectShow audio) and pushes FLV/RTMP to your ingest.
- The two talk over the `DirectStreamService` app service declared in the package manifest.
- A separate **Direct Stream Chat** Game Bar widget opens the chat page for Twitch, YouTube, Kick, Facebook, Rumble, VetTV, or a custom HTTPS chat URL. Twitch uses a channel name, YouTube uses a video ID/watch URL, and Kick uses a channel name. The remaining platforms accept an HTTPS live/chat page URL. Some sites may require browser sign-in; YouTube may require the external-browser fallback because its embedded chat can reject older UWP browser engines.

## Roadmap
- Per-source volume sliders (game/mic mixer v2; v1 mixes both at unity gain)

## Build from source
Visual Studio with the UWP workload, Windows SDK 10.0.26100, and the .NET 8 SDK. Add the assets in `DirectStreamWidget\Assets\ASSETS-REQUIRED.md`, then build Release | x64; the solution builds StreamerHost before packaging it with the widget. Deploy, then press Win+G. Details in INSTALL.md.

## License
MIT - Copyright (c) 2026 VetTV Studios
