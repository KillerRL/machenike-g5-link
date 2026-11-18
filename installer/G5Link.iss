#define AppName "G5 Link"
#define AppVersion "0.1.0"
#define AppExe "G5Link.exe"
[Setup]
AppId={{6BC34B62-BE1D-4267-BC08-C15801B43E9E}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=KillerRL
AppPublisherURL=https://github.com/KillerRL/machenike-g5-link
DefaultDirName={localappdata}\Programs\G5 Link
DefaultGroupName=G5 Link
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\installer-output
OutputBaseFilename=G5Link-Setup-0.1.0-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}
[Languages]
Name: "portuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
[Files]
Source: "..\installer-build\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\G5 Link"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{autodesktop}\G5 Link"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon; WorkingDir: "{app}"
[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; Flags: unchecked
[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir o G5 Link"; Flags: nowait postinstall skipifsilent
