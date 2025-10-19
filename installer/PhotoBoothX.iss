; PhotoBoothX Installer Script - Flag Testing: ALL REMAINING FLAGS
; Testing: Adding ALL remaining flags at once for speed

#define MyAppName "PhotoBoothX"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "PhotoBoothX"
#define MyAppURL "https://github.com/splenwilz/PhotoBoothX"
#define MyAppExeName "PhotoBooth.exe"
#define MyAppDescription "Professional Photobooth Kiosk Application"

[Setup]
; Basic Information
AppId={{8A7B5C6D-9E4F-4A5B-8C7D-9E5F6A7B8C9D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=..\dist
OutputBaseFilename=PhotoBoothX-Setup-{#MyAppVersion}
SetupIconFile=icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Testing: GroupDescription parameter
Name: "startupentry"; Description: "Launch {#MyAppName} automatically when Windows starts (Recommended for kiosk mode)"; GroupDescription: "Startup Options:"
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"

[Files]
; Application files (exclude sensitive files for security)
; Note: Database_Schema.sql is now embedded in DLL, *.pdb files excluded by build config
Source: "..\PhotoBooth\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Excludes: "*.config.template"; Flags: ignoreversion recursesubdirs createallsubdirs
; Templates
Source: "..\PhotoBooth\Templates\*"; DestDir: "{app}\Templates"; Flags: ignoreversion recursesubdirs createallsubdirs
; Master password config (Enterprise builds only - auto-deleted on first use)
; Special permissions set via [InstallDelete] and [Code] to allow app to delete it
#ifexist "..\PhotoBooth\master-password.config"
Source: "..\PhotoBooth\master-password.config"; DestDir: "{app}"; Flags: ignoreversion; Permissions: users-modify
#endif

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Dirs]
; Testing: Permissions parameter
Name: "{app}\Templates"; Permissions: everyone-modify

[Registry]
; Working flags + Testing Tasks parameter
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: startupentry; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"; Flags: uninsdeletekey

[Run]
; Testing: nowait, postinstall, skipifsilent flags
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Security: Clean up master password config file if it still exists
Type: files; Name: "{app}\master-password.config"

[UninstallRun]
; Testing: runhidden flag
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im ""{#MyAppExeName}"""; Flags: runhidden 