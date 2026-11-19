#define MyAppName "SATIŞÇIM"
#define MyAppVersion "1.0.6"
#define MyAppPublisher "SATIŞÇIM"
#define MyAppExeName "SATISCIM.exe"
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

[Setup]
AppId={{B7C9D4EA-5D88-4F2A-9C8D-4E0B2D8A6A11}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\SATISCIM
DefaultGroupName={#MyAppName}
OutputDir=..\setup-output
OutputBaseFilename=SATISCIM-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayName={#MyAppName}

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName} uygulamasını başlat"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
