#define AppVer GetEnv("APP_VERSION")
[Setup]
AppName=VetTV DirectStream
AppVersion={#AppVer}
AppPublisher=VetTV Studios
DefaultDirName={autopf}\VetTV DirectStream
DefaultGroupName=VetTV DirectStream
OutputDir=..\dist
OutputBaseFilename=VetTV-DirectStream-Setup-{#AppVer}
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\VetTVDirectStream.exe

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\VetTV DirectStream"; Filename: "{app}\VetTVDirectStream.exe"
Name: "{autodesktop}\VetTV DirectStream"; Filename: "{app}\VetTVDirectStream.exe"

[Run]
Filename: "{app}\VetTVDirectStream.exe"; Description: "Launch VetTV DirectStream"; Flags: nowait postinstall skipifsilent
