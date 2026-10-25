# Store submission kit — VetTV Direct Stream

Everything Partner Center asks for, in one place. Package identity
(fixed; do not change without a new reservation):

- Package/Identity/Name: `65128fromeister2009.VetTVDirectStream`
- Package/Identity/Publisher: `CN=FDC0BAA2-3C8B-4D81-BC95-81C9D1954673`
- Publisher display name: `Vet GamingTV`
- Category: Entertainment (or Utilities & tools) · Age rating: complete
  the IARC questionnaire (creator tool; user-generated live content)

## 1. Get the upload package

1. GitHub > Actions > **Store Package (.msixupload)** > Run workflow
   (or push a `v*` tag). It verifies the manifest identity, bundles the
   pinned SHA256-verified ffmpeg, and builds the unsigned upload bundle.
2. Download the `DirectStreamWidget-StoreUpload` artifact. It must
   contain exactly one `.msixupload` (>80 MB; ffmpeg is inside).
3. Optional but recommended before uploading: install the package from
   a local Store-mode build in Visual Studio (**Publish > Create App
   Packages > Microsoft Store**) and run the **Windows App
   Certification Kit (WACK)** against it. Fix every failure first.

## 2. Partner Center submission fields

- **Packages:** upload the `.msixupload`. Confirm Partner Center shows
  Name/Publisher above and version `1.2.0.0`, architecture x64,
  min OS Windows 10 19041. Delete any other package in the submission.
- **Store listing (en-us):** copy from [LISTING.md](LISTING.md).
  Upload the app icon (300x300 `Assets/Square300.png`) and at least
  one screenshot (see checklist below).
- **Privacy policy URL (required):** this app uses the microphone,
  screen capture and internet, so Partner Center requires a URL.
  Publish `PRIVACY.md` (GitHub Pages on this repo works: Settings >
  Pages) and paste its URL. Fill in the support email in PRIVACY.md
  and below first.
- **Support email / website:** vetgaming1982@outlook.com — use the same address in the
  listing, PRIVACY.md, and Partner Center.
- **Capability justifications:** paste from
  [CAPABILITY-JUSTIFICATIONS.md](CAPABILITY-JUSTIFICATIONS.md) if the
  submission form asks why restricted capabilities are declared.
- **Notes for certification:** paste from
  [TESTER-INSTRUCTIONS.md](TESTER-INSTRUCTIONS.md). Certification
  installs on a clean machine; everything needed is in the package.
- **Pricing and availability:** Free; all markets; public audience.
- **Properties:** confirm no gaming-services / Xbox Live claims; the
  app is a Game Bar widget, not a game.

## 3. Before you press Submit

- [ ] Support email filled in (PRIVACY.md + listing + Partner Center)
- [ ] Privacy policy URL live and pasted into Partner Center
- [ ] At least 1 screenshot (1366x768 or larger, actual app UI)
- [ ] WACK run passed on the exact `.msixupload` being submitted
- [ ] Manual clean-machine test: fresh Windows user, install, Win+G,
      pin **Direct Stream**, stream to a test/unlisted destination,
      pin **Direct Stream Chat**, load a chat page, Stop cleanly
- [ ] Stream key never appears in the on-screen log (redaction test)
- [ ] IARC age-rating questionnaire submitted
- [ ] Age/privacy declarations match PRIVACY.md (no data collection)

## 4. After certification

- GitHub Releases are no longer how users install: point README badges
  and links at the Store listing once it is live.
- For updates: bump `Version` in `Package.appxmanifest` (and
  AssemblyInfo), push a new `v*` tag, upload the new `.msixupload` as
  a new Partner Center submission. Identity must never change.
- The old self-signed sideload certificate was removed from this repo
  because its private key was committed publicly. Treat it as
  compromised; it is not used by Store builds.
