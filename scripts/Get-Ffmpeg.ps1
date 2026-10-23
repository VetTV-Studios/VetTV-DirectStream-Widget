<#
.SYNOPSIS
  Downloads a static ffmpeg build and places ffmpeg.exe where StreamerHost
  looks for it (next to StreamerHost.exe inside the installed package is not
  writable for Store-style installs, so by default this installs to
  C:\ffmpeg\bin, which is on StreamerHost's search list).

.EXAMPLE
  .\Get-Ffmpeg.ps1
#>
$ErrorActionPreference = "Stop"
$dest = "C:\ffmpeg"
$zip = Join-Path $env:TEMP "ffmpeg-release-essentials.zip"

Write-Host "Downloading ffmpeg (gyan.dev essentials build)..."
Invoke-WebRequest -Uri "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip" -OutFile $zip

Expand-Archive -Path $zip -DestinationPath $env:TEMP -Force
$bin = Get-ChildItem -Path $env:TEMP -Directory -Filter "ffmpeg-*-essentials_build" | Select-Object -First 1
if (-not $bin) { throw "Could not find extracted ffmpeg folder." }

New-Item -ItemType Directory -Force -Path "$dest\bin" | Out-Null
Copy-Item "$($bin.FullName)\bin\ffmpeg.exe" "$dest\bin\ffmpeg.exe" -Force
Write-Host "Installed ffmpeg.exe to $dest\bin\ffmpeg.exe"
& "$dest\bin\ffmpeg.exe" -hide_banner -version | Select-Object -First 1
