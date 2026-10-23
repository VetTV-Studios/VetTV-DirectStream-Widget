# Xbox Game Bar Direct Stream Widget

A functional Xbox Game Bar widget for direct RTMP streaming (Twitch, YouTube, Rumble, custom ingest like VetTV).

This repo contains a buildable UWP Game Bar widget + a FullTrust helper that launches FFmpeg to stream your desktop/game audio directly from the Game Bar overlay.

## What it does
- Appears in Game Bar (Win+G) -> Widget Menu
- Enter RTMP URL + Stream Key (saved securely in local settings)
- Choose Quality: 720p60 / 1080p60 / 1080p30 / Source
- One-click Start / Stop Streaming
- Shows live status, bitrate, duration, and FFmpeg log
- Mutes mic / includes desktop audio toggle
- Settings widget for default server, auto-reconnect, hardware encoder preference

## Architecture
- `DirectStreamWidget` (UWP, XAML) - The Game Bar widget UI. References Microsoft.Gaming.XboxGameBar SDK
- `StreamerHost` (Win32 .NET Console) - FullTrust process that runs FFmpeg. Communicates with widget via AppService

Why two projects? UWP cannot directly launch FFmpeg with desktop capture. The widget talks to the FullTrust process which does the heavy lifting.

## Requirements
- Windows 10 1903+ / Windows 11
- Visual Studio 2022 with UWP workload
- Windows 10 SDK 19041+
- FFmpeg (download from ffmpeg.org) - place ffmpeg.exe next to StreamerHost.exe or in PATH

## Build Steps
1. Open `XboxGameBarDirectStreamWidget.sln` in VS2022
2. Restore NuGet: `Microsoft.Gaming.XboxGameBar` (latest)
3. Right-click Solution -> Build (x64, Debug)
4. Set `DirectStreamWidget` as Startup, F5 to debug. Press Win+G to see widget.
   - Follow https://learn.microsoft.com/gaming/game-bar/quickstart/test-and-debug-your-widget for debugging setup

## Configuration
Edit `StreamingService.cs` if you want to change default encoders:
- Tries h264_nvenc -> h264_qsv -> libx264
- Default audio: virtual-audio-capturer or Stereo Mix fallback

## GitHub Setup (Push this folder)
```bash
cd XboxGameBarDirectStreamWidget
git init
git add .
git commit -m "Initial: Direct Stream Game Bar widget"
gh repo create VetTV-DirectStream-Widget --public --source=. --remote=origin --push
# or manually create repo on github.com and:
git remote add origin https://github.com/YOUR_USERNAME/REPO.git
git branch -M main
git push -u origin main
```

## Store Publishing
Once tested, package for Microsoft Store. See https://learn.microsoft.com/gaming/game-bar/quickstart/distributing-your-widget-application

## License: MIT
