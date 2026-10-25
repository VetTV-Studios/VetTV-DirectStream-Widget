# Bundled ffmpeg goes here

Run `scripts\Get-BundledFfmpeg.ps1` from the repo root before building.
It downloads and SHA256-verifies the pinned ffmpeg 7.1.1 build, then
places `ffmpeg.exe` and `LICENSE-ffmpeg.txt` in this folder.

`ffmpeg.exe` is intentionally not committed to git (see `.gitignore`);
CI runs the same script before packaging. The widget project links
`ffmpeg.exe` into the package as `StreamerHost\ffmpeg.exe`, beside
`StreamerHost.exe`, which is the first location `FfmpegArgs.FindFfmpeg`
checks at runtime.
