#ifndef AppVersion
  #error AppVersion must be provided with /DAppVersion=...
#endif

#ifndef Platform
  #error Platform must be provided with /DPlatform=x64 or /DPlatform=arm64
#endif

[Setup]
AppId={{9B6FC843-EC74-4E8D-AEF5-D095592599B3}
AppName=Zshot
AppVersion={#AppVersion}
AppPublisher=Zyl0812
AppPublisherURL=https://github.com/Zyl0812/Zshot
AppSupportURL=https://github.com/Zyl0812/Zshot/issues
AppUpdatesURL=https://github.com/Zyl0812/Zshot/releases
DefaultDirName={localappdata}\Programs\Zshot
DefaultGroupName=Zshot
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0
SourceDir=..
OutputDir=.
OutputBaseFilename=Zshot-{#AppVersion}-win-{#Platform}-setup
SetupIconFile=src\logo.ico
UninstallDisplayIcon={app}\Zshot.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
LicenseFile=LICENSE

#if Platform == "x64"
ArchitecturesAllowed=x64compatible and not arm64
ArchitecturesInstallIn64BitMode=x64compatible
#elif Platform == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
  #error Unsupported Platform
#endif

[Files]
Source: "build\release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Icons]
Name: "{group}\Zshot"; Filename: "{app}\Zshot.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Zshot"; Filename: "{app}\Zshot.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Zshot.exe"; Description: "Launch Zshot"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\app-*"
