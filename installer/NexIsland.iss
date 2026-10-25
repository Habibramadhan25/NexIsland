; NexIsland Inno Setup Script
; Generates standard Windows installer (EXE) for NexIsland

#define MyAppName "NexIsland"
#define MyAppVersion "0.1.0-beta.1"
#define MyAppPublisher "NexIsland Team"
#define MyAppURL "https://github.com/NexIsland/NexIsland"
#define MyAppExeName "NexIsland.exe"

[Setup]
AppId={{D377B841-A816-4FD6-B819-B5B309A3215C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist\installer
OutputBaseFilename=NexIsland-v{#MyAppVersion}-Setup
Compression=lzma2/ultra64
SolidCompression=yes
SetupIconFile=..\src\NexIsland\Assets\Branding\NexIslandIcon.ico
WizardStyle=modern
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start NexIsland automatically with Windows"; GroupDescription: "Windows Integration:"

[Files]
Source: "..\dist\NexIsland-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\Branding\NexIslandIcon.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\Assets\Branding\NexIslandIcon.ico"
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon; IconFilename: "{app}\Assets\Branding\NexIslandIcon.ico"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
