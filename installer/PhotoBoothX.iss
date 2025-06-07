; PhotoBoothX Installer Script - Minimal Test Version
; Testing flags one by one to identify the problematic flag

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
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Testing Tasks section - NO FLAGS FIRST
Name: "startupentry"; Description: "Launch {#MyAppName} automatically when Windows starts"
Name: "desktopicon"; Description: "Create a desktop icon"

[Files]
; Basic files - NO FLAGS FIRST
Source: "..\PhotoBooth\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Registry]
; Basic registry - NO FLAGS FIRST
Root: HKLM; Subkey: "SOFTWARE\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}" 