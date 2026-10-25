# Capability justifications — paste into Partner Center if asked

## runFullTrust (restricted)
VetTV Direct Stream is an Xbox Game Bar widget. The widget UI runs in
the Game Bar sandbox, which cannot capture the desktop or encode video.
A full-trust companion process (`StreamerHost.exe`, declared via
`windows.fullTrustProcess` in the same package) performs the single
task the product exists for: running the bundled FFmpeg encoder
(gdigrab desktop capture + DirectShow audio) and publishing the RTMP
stream the user explicitly starts. It exposes no UI, installs no
services, and only communicates with the widget through the package's
own `DirectStreamService` app service. Streaming stops, and FFmpeg is
terminated, when the user presses Stop or the widget closes.

## microphone (device capability)
Used only when the user enables microphone audio for their own live
broadcast. Audio is mixed into the stream the user started and sent
only to the RTMP destination the user selected. Nothing is recorded,
stored, or transmitted anywhere else.

## internetClient
Required to publish the user's live stream to the RTMP ingest server
of the platform the user selected (Twitch, YouTube, Facebook, Kick,
Rumble, VetTV, or a custom URL), and to display that platform's chat
page in the companion chat widget.

## Desktop screen capture (via full-trust FFmpeg gdigrab)
The broadcast video source is the user's own desktop/gameplay, started
and stopped explicitly by the user in the widget. Frames go only into
the encoder and out to the user's chosen stream destination.
