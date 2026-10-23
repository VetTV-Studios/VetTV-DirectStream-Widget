# Required package assets

The manifest and csproj reference these files. Packaging fails without them
(the scaffold shipped none of them - diagnosis item 6). Generate them in the
VH neon branding: background #0E0520, cyan #00D9FF, pink #FF2E9A. A simple
"DS" monogram or the VetTV wordmark centered on #0E0520 is enough for v1.1.

| File | Size | Used for |
|---|---|---|
| StoreLogo.png | 50x50 | Package logo (Properties) |
| Square150x150Logo.png | 150x150 | VisualElements tile logo |
| SmallLogo.png | 44x44 | VisualElements Square44x44Logo |
| WideTile.png | 310x150 | DefaultTile Wide310x150Logo |
| SplashScreen.png | 620x300 | Splash screen |
| widget_icon.png | 48x48 | Game Bar widget icon reference |

Keep the filenames exactly as above and place the PNGs in this folder,
replacing this note's folder contents (this .md may stay; it is not packaged
because the csproj only includes the listed PNGs).
