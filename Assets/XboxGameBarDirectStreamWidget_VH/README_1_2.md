# Xbox Game Bar Direct Stream Widget v2 - Multi-Platform + Chat + RTX

Full-featured Game Bar widget for direct RTMP streaming to **Twitch, YouTube, Facebook, Kick, Rumble + Custom (VetTV)**

## New in v2
- **5 Platform Presets**: Twitch, YouTube, Facebook, Kick, Rumble + Custom RTMP - auto-fills RTMP URL and help text
- **Chat Overlay Widget**: Second Game Bar widget showing live chat via popout WebView (Twitch popout, YouTube live_chat, Kick/Rumble popouts)
- **RTX NVENC Encoder Options**:
  - Auto (picks best), H.264 RTX, HEVC RTX (40% more efficient), QSV, AMF, x264 fallback
  - RTX Features toggle: Lookahead 32, Psycho Visual Tuning, Spatial/Temporal AQ, B-Ref Mode
  - Optimized presets p5/p7 for low-latency streaming
- **Resolutions**: 720p30 (3.5M), 720p60 (4.5M), 1080p30 (4.5M), 1080p60 (6M), 1440p30 (8M), 1440p60 (12M RTX)
- **Audio Mixer**: Separate sliders for Game Audio (virtual-audio-capturer) and Mic with volume % + include toggles + mute
  - FFmpeg amix filter mixes at specified volumes, no external mixer needed
- **Platform-aware Chat**: Enter Channel/Video ID and hit 💬 CHAT to open chat overlay widget

## Architecture
- DirectStreamWidget (UWP) -> Widget1.xaml (main) + ChatOverlay.xaml (chat)
- StreamingService.cs builds ffmpeg args with volume and NVENC tuning
- PlatformPresets.cs dictionary holds RTMP + chat URLs
- StreamerHost (FullTrust) runs ffmpeg with args

## RTMP Endpoints (built-in)
- Twitch: rtmp://live.twitch.tv/app
- YouTube: rtmp://a.rtmp.youtube.com/live2
- Facebook: rtmps://live-api-s.facebook.com:443/rtmp/
- Kick: rtmp://fa723fc1b171.global-contribute.live-video.net/app/
- Rumble: rtmp://4d37a1b1b5a8.global-contribute.live-video.net/app/ (same infra as Kick)

## How Audio Mixer Works
- GameVol slider 0-100% -> ffmpeg -filter:a volume=0.8
- MicVol slider 0-100% -> second filter
- When both enabled: -filter_complex "[0:a]volume=0.8[a1];[1:a]volume=0.85[a2];[a1][a2]amix=inputs=2" mixes them
- Mute checkbox sets micVol=0 without losing slider position

## RTX NVENC Setup
1. Install latest NVIDIA Studio/Game Ready drivers
2. Select NVENC RTX Auto or HEVC RTX
3. Enable "RTX: Lookahead + Psycho Tuning" for best quality
4. For 1440p60, use HEVC RTX @ 12Mbps - saves ~30% bandwidth vs H.264

## Build
Same as v1: VS2022, UWP workload, place ffmpeg.exe next to exe.

## GitHub Push
See previous README.
