# Third-Party Notices

## FFmpeg 7.1.1 (essentials build, GyanD / codexffmpeg)

- Bundled binary: `ffmpeg.exe`, placed beside `StreamerHost.exe` in the package.
- Source: https://github.com/GyanD/codexffmpeg/releases/tag/7.1.1
  (pinned asset `ffmpeg-7.1.1-essentials_build.zip`,
  SHA256 `04861D3339C5EBE38B56C19A15CF2C0CC97F5DE4FA8910E4D47E5E6404E4A2D4`,
  fetched and verified by `scripts/Get-BundledFfmpeg.ps1`).
- License: GNU General Public License v3 (`--enable-gpl --enable-version3`).
  The full license text ships in the package as
  `StreamerHost\LICENSE-ffmpeg.txt`.
- Source code: https://ffmpeg.org/download.html and
  https://github.com/FFmpeg/FFmpeg (tag `n7.1.1`).
- FFmpeg is a trademark of Fabrice Bellard. This project is not affiliated
  with or endorsed by the FFmpeg project.
- Libraries linked into this build include libx264 (GPL), x265, libvpx,
  libopus, libvorbis, libmp3lame, AMF, NVENC/NVDEC headers and Intel
  oneVPL, each under its own license; see the FFmpeg `LICENSE` file and
  https://ffmpeg.org/legal.html for the complete list.

## Microsoft Gaming Xbox Game Bar SDK

- NuGet: `Microsoft.Gaming.XboxGameBar` 7.3.2607010, (c) Microsoft,
  licensed under the terms of its NuGet package.
