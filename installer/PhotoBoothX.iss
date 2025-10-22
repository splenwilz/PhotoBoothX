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
; SECURITY NOTES:
;   - Database_Schema.sql is embedded in DLL as resource (not present in published files)
;   - *.pdb debug symbols excluded by build configuration
;   - *.config.template files excluded to prevent confusion
;   - Master password config (if present) will be auto-deleted after first use
Source: "..\PhotoBooth\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Excludes: "*.config.template;master-password.config"; Flags: ignoreversion recursesubdirs createallsubdirs
; Master password config - conditionally included in ProgramData (not Program Files)
; Placed in {commonappdata} to avoid weakening Program Files security
; File permissions set to allow app to delete it after loading into encrypted database
#ifexist "..\PhotoBooth\bin\Release\net8.0-windows\win-x64\publish\master-password.config"
Source: "..\PhotoBooth\bin\Release\net8.0-windows\win-x64\publish\master-password.config"; DestDir: "{commonappdata}\PhotoBoothX"; Flags: ignoreversion; Permissions: users-modify
#endif
; Templates
Source: "..\PhotoBooth\Templates\*"; DestDir: "{app}\Templates"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Dirs]
; Testing: Permissions parameter
Name: "{app}\Templates"; Permissions: everyone-modify
; Create ProgramData directory for machine-wide kiosk data (database, logs, temp config)
Name: "{commonappdata}\PhotoBoothX"; Permissions: users-modify
Name: "{commonappdata}\PhotoBoothX\Logs"; Permissions: users-modify

[Registry]
; Working flags + Testing Tasks parameter
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: startupentry; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"; Flags: uninsdeletekey

[Run]
; Testing: nowait, postinstall, skipifsilent flags
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up master password config if app failed to delete it (security backup)
Type: files; Name: "{commonappdata}\PhotoBoothX\master-password.config"
; Clean up ProgramData directory if empty
Type: dirifempty; Name: "{commonappdata}\PhotoBoothX"
; No other temporary files to clean up - all user data preserved in AppData

[UninstallRun]
; Testing: runhidden flag
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im ""{#MyAppExeName}"""; Flags: runhidden 