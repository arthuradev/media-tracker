#define AppName "Media Tracker"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif
#ifndef AppIcon
  #define AppIcon "..\src\MediaTracker\Assets\AppIcon.ico"
#endif

[Setup]
AppId={{A7ACB5E0-D639-43CC-8D98-10FF1B9186E4}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Arthur
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=MediaTracker-setup-{#AppVersion}
SetupIconFile={#AppIcon}
UninstallDisplayIcon={app}\MediaTracker.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\MediaTracker.exe"; IconFilename: "{app}\MediaTracker.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\MediaTracker.exe"; Tasks: desktopicon; IconFilename: "{app}\MediaTracker.exe"

[Run]
Filename: "{app}\MediaTracker.exe"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
