# VetTV Direct Stream Widget

Xbox Game Bar widget for direct RTMP streaming (Win+G). One-click Go Live from Game Bar - no OBS needed.

**Platforms:** Twitch, YouTube, Facebook, Kick, Rumble, and VetTV ingest built in as presets, plus any custom RTMP URL.
**Encoders:** NVIDIA NVENC, Intel QSV, AMD AMF, or x264 - with real auto-detection and fallback based on the ffmpeg build you have installed.
**Quality:** 720p / 1080p / 1440p at 30 or 60 fps, 2-second keyframe interval as the platforms require.
**Audio:** desktop/game audio and microphone mixed into one AAC track (via ffmpeg + DirectShow).

## Install
See [INSTALL.md](INSTALL.md). Short version: download the `.msix` from Releases, trust the VetTV Studios signing certificate once, install ffmpeg (`scripts\Get-Ffmpeg.ps1`), install the package, then Win+G and pin **Direct Stream**.

## How it works
- The widget (UWP) is just the UI: platform/quality/encoder picks, stream key (kept in the Windows Credential Vault), status and log.
- A full-trust companion process, **StreamerHost**, ships in the same package and does the actual work: it runs ffmpeg (gdigrab desktop capture + DirectShow audio) and pushes FLV/RTMP to your ingest.
- The two talk over the `DirectStreamService` app service declared in the package manifest.

## Roadmap (not in this release)
- Live chat overlay widget
- Per-source volume sliders (game/mic mixer v2; v1 mixes both at unity gain)

## Build from source
Visual Studio 2022 + UWP workload + Windows SDK 10.0.22621. Add the assets in `DirectStreamWidget\Assets\ASSETS-REQUIRED.md`, build Release | x64, deploy, Win+G. Details in INSTALL.md.

## License
MIT - Copyright (c) 2026 VetTV Studios
