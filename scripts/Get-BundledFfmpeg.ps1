<#
.SYNOPSIS
  Downloads the pinned ffmpeg build bundled into the Store package and
  places ffmpeg.exe where the package expects it:
  StreamerHost\ffmpeg\ffmpeg.exe (linked into the package as
  StreamerHost\ffmpeg.exe, next to StreamerHost.exe).

.DESCRIPTION
  Pinned for reproducibility. The SHA256 below was computed from the
  official GyanD/codexffmpeg GitHub release asset on 2026-10-25.
  ffmpeg 7.1.1 essentials is a GPLv3 static build; it provides every
  encoder the widget advertises (h264_nvenc, h264_qsv, h264_amf,
  libx264), the gdigrab/dshow capture devices and the FLV muxer.
  Its LICENSE ships in the package (see THIRD-PARTY-NOTICES.md).
  ffmpeg.exe itself is never committed to git (.gitignore covers it);
  CI runs this script before packaging.

.EXAMPLE
  .\scripts\Get-BundledFfmpeg.ps1
#>
$ErrorActionPreference = "Stop"

$version = "7.1.1"
$url = "https://github.com/GyanD/codexffmpeg/releases/download/$version/ffmpeg-$version-essentials_build.zip"
$expectedSha256 = "04861D3339C5EBE38B56C19A15CF2C0CC97F5DE4FA8910E4D47E5E6404E4A2D4"

$repoRoot = Split-Path $PSScriptRoot -Parent
$destDir = Join-Path $repoRoot "StreamerHost\ffmpeg"
$zip = Join-Path $env:TEMP "ffmpeg-$version-essentials_build.zip"
$extract = Join-Path $env:TEMP "ffmpeg-$version-bundled"

Write-Host "Downloading ffmpeg $version (pinned, SHA256-verified)..."
Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing

$actual = (Get-FileHash $zip -Algorithm SHA256).Hash
if ($actual -ne $expectedSha256) {
    throw "ffmpeg zip SHA256 mismatch. Expected $expectedSha256, got $actual. Refusing to bundle an unverified binary."
}
Write-Host "SHA256 verified: $actual"

if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
Expand-Archive -Path $zip -DestinationPath $extract -Force
$bin = Get-ChildItem $extract -Directory -Filter "ffmpeg-*-essentials_build" | Select-Object -First 1
if (-not $bin) { throw "Could not find extracted ffmpeg folder." }

New-Item -ItemType Directory -Force -Path $destDir | Out-Null
Copy-Item (Join-Path $bin.FullName "bin\ffmpeg.exe") (Join-Path $destDir "ffmpeg.exe") -Force
Copy-Item (Join-Path $bin.FullName "LICENSE") (Join-Path $destDir "LICENSE-ffmpeg.txt") -Force

& (Join-Path $destDir "ffmpeg.exe") -hide_banner -version | Select-Object -First 1
Write-Host "Bundled ffmpeg.exe + LICENSE-ffmpeg.txt -> $destDir"
