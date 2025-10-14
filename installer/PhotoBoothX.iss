; PhotoBoothX Installer Script - With Secure Credential Generation
; Generates random passwords during installation and creates accessible credentials file

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
; All Files flags work fine
Source: "..\PhotoBooth\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Additional files
Source: "..\PhotoBooth\Templates\*"; DestDir: "{app}\Templates"; Flags: ignoreversion recursesubdirs createallsubdirs

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

[UninstallRun]
; Testing: runhidden flag
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im ""{#MyAppExeName}"""; Flags: runhidden

[Code]
var
  MasterPassword: String;
  UserPassword: String;
  CredentialsFilePath: String;

// Generate a random password with specified length
function GenerateRandomPassword(Length: Integer): String;
var
  i: Integer;
  CharSet: String;
  RandomIndex: Integer;
begin
  // Character set: uppercase, lowercase, numbers, and safe symbols
  CharSet := 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*';
  Result := '';
  
  // Seed random number generator with current time
  Randomize;
  
  for i := 1 to Length do
  begin
    RandomIndex := Random(Length(CharSet)) + 1;
    Result := Result + Copy(CharSet, RandomIndex, 1);
  end;
end;

// Generate credentials during installation
procedure GenerateCredentials();
begin
  // Generate strong random passwords
  MasterPassword := GenerateRandomPassword(16);
  UserPassword := GenerateRandomPassword(16);
  
  // Set credentials file path to application directory for clean organization
  CredentialsFilePath := ExpandConstant('{app}\setup-credentials.txt');
end;

// Create credentials file with generated passwords
function CreateCredentialsFile(): Boolean;
var
  CredentialsContent: String;
  CurrentDateTime: String;
begin
  Result := False;
  
  try
    // Get current date/time for the file
    CurrentDateTime := GetDateTimeString('yyyy-mm-dd hh:nn:ss', #0, #0);
    
    // Build credentials file content
    CredentialsContent := 
      'PhotoBoothX - Initial Setup Credentials' + #13#10 +
      'Generated: ' + CurrentDateTime + #13#10 + #13#10 +
      
      '⚠️  IMPORTANT SECURITY NOTICE:' + #13#10 +
      '- These are ONE-TIME setup credentials' + #13#10 +
      '- CHANGE THESE PASSWORDS immediately after first login' + #13#10 +
      '- DELETE this file after completing setup' + #13#10 +
      '- Keep these credentials secure until setup is complete' + #13#10 + #13#10 +
      
      'Master Administrator Account:' + #13#10 +
      '  Username: admin' + #13#10 +
      '  Password: ' + MasterPassword + #13#10 +
      '  Access: Full admin panel access' + #13#10 + #13#10 +
      
      'Operator Account:' + #13#10 +
      '  Username: user' + #13#10 +
      '  Password: ' + UserPassword + #13#10 +
      '  Access: Limited access (reports, volume control)' + #13#10 + #13#10 +
      
      'Setup Instructions:' + #13#10 +
      '1. Start PhotoBoothX application' + #13#10 +
      '2. Tap 5 times in top-left corner of welcome screen' + #13#10 +
      '3. Login with credentials above' + #13#10 +
      '4. Immediately change both passwords' + #13#10 +
      '5. Delete this file after successful setup' + #13#10 + #13#10 +
      
      'Security Best Practices:' + #13#10 +
      '- Use strong passwords (12+ characters, mixed case, numbers, symbols)' + #13#10 +
      '- Enable password rotation reminders' + #13#10 +
      '- Limit operator account permissions' + #13#10 +
      '- Regularly backup admin settings' + #13#10 + #13#10 +
      
      '⚠️  Please delete this file after completing admin setup.' + #13#10 +
      '📁 File Location: ' + CredentialsFilePath + #13#10;
    
    // Write credentials file to C:\ root
    if SaveStringToFile(CredentialsFilePath, CredentialsContent, False) then
    begin
      Result := True;
      Log('Credentials file created successfully at: ' + CredentialsFilePath);
    end
    else
    begin
      Log('Failed to create credentials file at: ' + CredentialsFilePath);
    end;
    
  except
    Log('Exception occurred while creating credentials file');
    Result := False;
  end;
end;

// Store credentials in registry for application to read
procedure StoreCredentialsInRegistry();
begin
  try
    // Store the generated passwords in registry for the application to use
    RegWriteStringValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\' + '{#MyAppPublisher}' + '\' + '{#MyAppName}' + '\Setup', 
                       'MasterPassword', MasterPassword);
    RegWriteStringValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\' + '{#MyAppPublisher}' + '\' + '{#MyAppName}' + '\Setup', 
                       'UserPassword', UserPassword);
    RegWriteStringValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\' + '{#MyAppPublisher}' + '\' + '{#MyAppName}' + '\Setup', 
                       'CredentialsFilePath', CredentialsFilePath);
    
    Log('Credentials stored in registry successfully');
  except
    Log('Failed to store credentials in registry');
  end;
end;

// Called during installation process
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    Log('Starting credential generation process...');
    
    // Generate random passwords
    GenerateCredentials();
    
    // Create credentials file
    if CreateCredentialsFile() then
    begin
      Log('Credentials file created successfully');
      
      // Store credentials in registry for application use
      StoreCredentialsInRegistry();
      
      // Show success message to user
      MsgBox('Setup Complete!' + #13#10 + #13#10 +
             'Initial login credentials have been created in:' + #13#10 +
             'Application folder → setup-credentials.txt' + #13#10 + #13#10 +
             'Full path: ' + CredentialsFilePath + #13#10 + #13#10 +
             '⚠️  IMPORTANT: Change these passwords immediately after first login!' + #13#10 +
             'The credentials file will auto-delete after password change.', 
             mbInformation, MB_OK);
    end
    else
    begin
      // Show error message if file creation failed
      MsgBox('Warning: Could not create credentials file.' + #13#10 + #13#10 +
             'Default credentials will be used:' + #13#10 +
             'Username: admin, Password: admin123' + #13#10 +
             'Username: user, Password: user123' + #13#10 + #13#10 +
             '⚠️  CHANGE THESE IMMEDIATELY after first login!', 
             mbCriticalError, MB_OK);
    end;
  end;
end; 