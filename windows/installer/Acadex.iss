#define MyAppName "Acadex"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "Acadex"
#define MyAppExeName "Acadex.Windows.exe"

[Setup]
AppId={{69C1C4E1-20A9-423B-9F30-6B714CA2E010}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Acadex
DefaultGroupName=Acadex
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=Acadex-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
CloseApplications=yes
RestartApplications=no

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\Acadex"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Acadex"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Acadex"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
; Intentionally do not delete LocalAppData user data (session/settings) on uninstall.
