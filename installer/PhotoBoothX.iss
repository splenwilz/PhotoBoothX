; PhotoBoothX Installer Script
; Inno Setup Script for Professional Photobooth Kiosk Application

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
LicenseFile=..\EULA.txt
PrivilegesRequired=admin
OutputDir=..\dist
OutputBaseFilename=PhotoBoothX-Setup-{#MyAppVersion}
; SetupIconFile=icon.ico  ; Add icon.ico file to enable custom installer icon
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

; Kiosk-friendly installer settings
AllowNoIcons=yes
DisableReadyMemo=no
DisableStartupPrompt=yes
ShowLanguageDialog=no

; Version Information
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppDescription}
VersionInfoCopyright=© 2024 {#MyAppPublisher}. All rights reserved.
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startupentry"; Description: "Launch {#MyAppName} automatically when Windows starts (Recommended for kiosk mode)"; GroupDescription: "Startup Options:"; Flags: checkedbydefault
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
; Main application files
Source: "..\PhotoBooth\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Templates folder - user-updatable
Source: "..\PhotoBooth\Templates\*"; DestDir: "{app}\Templates"; Flags: ignoreversion recursesubdirs createallsubdirs uninsneveruninstall
; Database schema
Source: "..\PhotoBooth\Database_Schema.sql"; DestDir: "{app}"; Flags: ignoreversion
; License files
Source: "..\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\EULA.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Auto-launch registry entry
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: startupentry; Flags: uninsdeletevalue

; Application registration
Root: HKLM; Subkey: "SOFTWARE\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "InstallDate"; ValueData: "{code:GetDateTimeString}"; Flags: uninsdeletekey

[Dirs]
; Ensure Templates directory has proper permissions for updates
Name: "{app}\Templates"; Permissions: everyone-modify

[Run]
; Launch application after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Stop application before uninstall
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im ""{#MyAppExeName}"""; Flags: runhidden; RunOnceId: "StopPhotoBoothX"

[Code]
// Custom functions for installation

// Get current date/time for registry
function GetDateTimeString(Param: String): String;
begin
  Result := GetDateTimeString('yyyy-mm-dd hh:nn:ss', #0, #0);
end;

// Check if application is running
function IsAppRunning(): Boolean;
var
  FWMIService: Variant;
  FSWbemLocator: Variant;
  FWbemObjectSet: Variant;
begin
  Result := false;
  try
    FSWbemLocator := CreateOleObject('WBEMScripting.SWBemLocator');
    FWMIService := FSWbemLocator.ConnectServer('', 'root\CIMV2', '', '');
    FWbemObjectSet := FWMIService.ExecQuery('SELECT Name FROM Win32_Process WHERE Name="' + '{#MyAppExeName}' + '"');
    Result := (FWbemObjectSet.Count > 0);
  except
    Result := false;
  end;
end;

// Pre-installation checks
function InitializeSetup(): Boolean;
begin
  Result := true;
  
  // Check if app is running and ask to close it
  if IsAppRunning() then
  begin
    if MsgBox('{#MyAppName} is currently running. Please close the application before continuing with the installation.' + #13#10 + #13#10 + 'Click OK to continue after closing the application, or Cancel to exit the installer.', 
              mbConfirmation, MB_OKCANCEL or MB_ICONQUESTION) = IDCANCEL then
    begin
      Result := false;
      Exit;
    end;
  end;
end;

// Post-installation setup
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Create application data directory
    CreateDir(ExpandConstant('{userappdata}\PhotoboothX'));
    
    // Set permissions for Templates directory to allow updates
    // This is handled by the [Dirs] section above
    
    // Log installation
    Log('PhotoBoothX installation completed successfully');
  end;
end;

// Pre-uninstall cleanup
function InitializeUninstall(): Boolean;
begin
  Result := true;
  
  // Confirm uninstall
  if MsgBox('Are you sure you want to completely remove {#MyAppName} and all of its components?', 
            mbConfirmation, MB_YESNO or MB_ICONQUESTION) = IDNO then
  begin
    Result := false;
    Exit;
  end;
  
  // Stop the application if running
  if IsAppRunning() then
  begin
    MsgBox('Stopping {#MyAppName} application...', mbInformation, MB_OK);
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im "{#MyAppExeName}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

// Post-uninstall cleanup
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDataPath: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Ask if user wants to remove application data
    AppDataPath := ExpandConstant('{userappdata}\PhotoboothX');
    if DirExists(AppDataPath) then
    begin
      if MsgBox('Do you want to remove all application data including database, settings, and custom templates?' + #13#10 + #13#10 + 
                'This will permanently delete all sales data and customizations.', 
                mbConfirmation, MB_YESNO or MB_ICONQUESTION) = IDYES then
      begin
        DelTree(AppDataPath, True, True, True);
      end;
    end;
  end;
end; 