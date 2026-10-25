# Notes for certification — paste into Partner Center

## What this app is
An Xbox Game Bar widget (Win+G) for live RTMP streaming, plus a
companion pinnable chat widget. There is no standalone window: after
install, the app appears only inside Xbox Game Bar. This is by design
(`AppListEntry="none"`).

## How to test (no account or external setup required)
1. Install the package on a clean Windows 10 19041+ / Windows 11 x64
   machine. Nothing else is needed: the encoder (FFmpeg) is bundled
   in the package; do not install anything from the web.
2. Press **Win+G**, open the widget menu, and pin **Direct Stream**.
3. To verify streaming without a real channel: choose **Custom RTMP**
   (or any preset), enter any ingest URL/key values, pick 720p30, and
   press **START STREAMING**. The status/log panel reports encoder
   start and connection results. Press **STOP**; the log confirms the
   encoder exited. Stream keys are redacted in the log automatically.
4. Pin **Direct Stream Chat** from the same widget menu, enter a
   public Twitch/Kick channel name (for example a high-traffic public
   channel), and press **LOAD CHAT**.
5. Microphone permission is requested only if mic audio is enabled.
   Desktop-audio capture reports a clear log message if the test
   machine lacks a virtual-audio-capturer device; video streaming is
   unaffected.

## Declarations
- No sign-in is required or offered by the app itself. Embedded chat
  pages belong to third-party platforms and may offer their own
  optional sign-in inside the web view.
- No in-app purchases, ads, or data collection. The full-trust
  companion process exists solely to run the bundled encoder at the
  user's request (see capability justifications).
