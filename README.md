# VetTV DirectStream

Stream your Windows desktop/game straight to Twitch, YouTube, Facebook, Kick, Rumble or the VetTV ingest.
Hardware encoding (NVIDIA NVENC, AMD AMF, Intel QSV, CPU fallback), 720p/1080p/1440p at 30/60 fps,
and a built-in game + mic mixer with level meters.

## Install
1. Go to **Releases** and download `VetTV-DirectStream-Setup-x.y.z.exe`.
2. Run it (no admin needed). Windows SmartScreen may warn because the app isn't code-signed yet: **More info → Run anyway**.
3. Open VetTV DirectStream, pick your platform, paste your stream key, press **Go Live**.

A portable `.zip` is also attached to each release. FFmpeg is bundled.

## Notes
- Stream keys are encrypted with Windows DPAPI for your account and never written to the log.
- Auto bitrate: 720p 3.5/4.5 Mbps, 1080p 6/8 Mbps, 1440p 9/12 Mbps (30/60 fps). Twitch caps around 6-8 Mbps; lower it manually if rejected.
- Kick/Rumble/VetTV: copy the ingest URL from your dashboard into Server URL.
- Captures the primary desktop. For games, use borderless/windowed-fullscreen.

## Build / release
```
dotnet publish src/VetTVDirectStream -c Release -r win-x64 --self-contained
git tag v1.0.0 && git push origin v1.0.0   # GitHub Actions builds installer + zip and publishes the release
```
MIT licensed. FFmpeg (GPL build) is downloaded at release time from BtbN/FFmpeg-Builds.
