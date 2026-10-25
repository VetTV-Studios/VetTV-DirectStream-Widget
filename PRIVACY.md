# Privacy Policy — VetTV Direct Stream

Effective date: 2026-10-25 · Publisher: Vet GamingTV

VetTV Direct Stream ("Direct Stream") is an Xbox Game Bar widget for
Windows that broadcasts your gameplay to a streaming platform you choose
(Twitch, YouTube, Facebook, Kick, Rumble, VetTV, or a custom RTMP server).
This policy describes exactly what the app does with your information.
**The app has no backend of its own: Vet GamingTV operates no server
that receives, stores, or sells your personal data, and the app contains
no analytics, advertising, or telemetry SDKs.**

## What the app accesses, and why

| Data | How it is used | Where it goes |
|---|---|---|
| Desktop/game screen | Captured locally and encoded as the video of your broadcast | Only to the RTMP ingest URL **you** select, as your live stream |
| Microphone audio | Mixed into your broadcast when you enable it | Only to the RTMP ingest URL you select |
| Desktop/game audio | Mixed into your broadcast when you enable it | Only to the RTMP ingest URL you select |
| Stream key | Authenticates your broadcast with your chosen platform | Sent only to that platform's ingest server; stored on your device (below) |
| Settings (platform, quality, encoder, chat channel/URL) | Remembers your preferences between sessions | Stored only on your device (Windows app LocalSettings) |
| Diagnostic log | Helps you troubleshoot failed streams | Stored only on your device; stream keys and RTMP targets are redacted before logging |

## Storage on your device

- **Stream key:** when "Save stream key" is on in Settings, the key is
  stored in the Windows Credential Manager (PasswordVault). When the
  toggle is off, the key is not persisted. The key is never written to
  plaintext settings files by the app.
- **Settings and logs** live in the app's private Windows storage and
  are removed when you uninstall the app.
- Nothing is synced to Vet GamingTV or any cloud service by this app.

## Third parties

- Your broadcast (video, audio, and stream key) goes directly from your
  PC to the streaming platform **you** pick. That platform's privacy
  policy governs what it does with your stream.
- The chat widget displays the chat page of your chosen platform in an
  embedded browser view. Any sign-in, cookies, or tracking on those
  pages are controlled by that platform, not by this app.
- The app bundles the open-source FFmpeg encoder (GPLv3; see
  THIRD-PARTY-NOTICES.md in the package). FFmpeg runs locally and makes
  no network connections except the RTMP stream you start.

## Children

The app is a creator tool intended for users who hold accounts on
streaming platforms; it does not knowingly collect data from children.

## Changes and contact

Material changes to this policy will be posted at the same address as
this document with a new effective date. Questions:
**vetgaming1982@outlook.com** (the same
address you enter in Partner Center > Store listing > Support contact).
