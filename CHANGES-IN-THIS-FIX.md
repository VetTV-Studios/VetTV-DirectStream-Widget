# Fix pass - what changed and why (maps to hidden/DIAGNOSIS.md items 1-15)

This `fixed/` tree is a drop-in replacement for the repo root. On push:
- Copy every file here over the repo root (same relative paths).
- DELETE `.github/workflows/dotnet-desktop.yml` (replaced by `build-release.yml`).
- KEEP the repo's existing `LICENSE` and `.gitignore` (unchanged).
- The repo's only remaining build input: the six PNGs in
  `DirectStreamWidget/Assets/ASSETS-REQUIRED.md`.

| Diagnosis item | Fix |
|---|---|
| 1. Stock-template workflow | Deleted; new `build-release.yml` builds the real solution on windows-2022, packages x64, publishes a GitHub Release on version tags |
| 2. csproj had no UWP targets; missing AssemblyInfo | Rewritten csproj imports `Microsoft.Windows.UI.Xaml.CSharp.targets`; `Properties/AssemblyInfo.cs` added |
| 3. .sln bogus GUIDs, no configuration mappings | New .sln: real C# project-type GUID `{FAE04EC0-...}`, fresh project GUIDs, full ProjectConfigurationPlatforms |
| 4. Game Bar SDK 6.1.0 | Bumped to 7.3.2607010 |
| 5. No packaging path | VS packaging via the UWP project itself (AppxPackageDir + GenerateAppxPackageOnBuild in CI); sideload .msix is the release artifact |
| 6. No Assets/ | `Assets/ASSETS-REQUIRED.md` lists the six required PNGs with exact sizes - THE ONE REMAINING INPUT |
| 7. Placeholder publisher/GUIDs | Publisher is now `CN=VetTV Studios` everywhere; `scripts/New-SigningCert.ps1` creates the matching self-signed cert; PhoneIdentity GUID replaced |
| 8. Manifest Game Bar extension | Kept the IDs (DirectStreamMain/DirectStreamSettings) and aligned with the documented uap3 GameBarWidget shape; AppListEntry="none" |
| 9. App-service plumbing missing | Manifest: `windows.appService` + `windows.fullTrustProcess` (StreamerHost) + `runFullTrust`. App: OnBackgroundActivated relay (Register Role=Host/Widget; forwards Start/Stop, relays Log/Status). Widget launches host via FullTrustProcessLauncher |
| 10. Demo-mode fake LIVE | Removed. StreamingService returns/throws the real error; UI shows "Error - see log" |
| 11. Broken ffmpeg builders (x2) | One builder: StreamerHost/FfmpegArgs.cs. Resolution+FPS applied via scale filter (incl. 1440p x6 presets), per-encoder presets (NVENC p4 / QSV medium / AMF balanced / x264 veryfast), valid multi-input dshow + amix, no hardcoded Realtek mic (devices enumerated from ffmpeg), real Auto encoder detection, FindFfmpeg searches PATH |
| 12. PlatformPresets.cs missing | Created: VetTV (rtmp://ingest.vettv.com/live), Twitch, YouTube, Facebook presets; Kick/Rumble presets intentionally blank-URL because both issue per-channel ingest URLs |
| 13. Dead settings; "(encrypted)" lie | SettingsWidget now persists DefaultRtmp/AutoReconnect/SaveKey/PreferredFps and Widget1 consumes them; stream key stored in PasswordVault; labels corrected |
| 14. Unimplemented chat overlay / mixer claims | README no longer claims them; moved to Roadmap. Audio is a real game+mic amix in v1.1 |
| 15. Wrong Quick Start (F5) | README + INSTALL.md: deploy package, then Win+G; cert trust + ffmpeg steps included |

Version: 1.1.0.0 (manifest, AssemblyInfo). Tag `v1.1.0` to cut the first release after pushing.

One verification item left for the first real build: the manifest's
proxyStub section - current SDK packages generate the interface registrations
at build time; if the widget fails to appear in Game Bar, copy the
`<Interface>` entries from Microsoft's official Xbox Game Bar widget sample
for SDK 7.3.x verbatim (noted in the manifest itself).
