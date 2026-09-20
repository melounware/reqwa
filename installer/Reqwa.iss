; Reqwa — Inno Setup script.
; Build the payload first:
;   dotnet publish -c Release -r win-x64 --self-contained true -o installer\app
; Compile:
;   ISCC.exe installer\Reqwa.iss   (Setup lands in installer\output)

#define MyAppName "Reqwa"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Reqwa"
#define MyAppExeName "ReqwaColors.exe"

[Setup]
AppId={{8C88F2FB-54A1-4AC2-A15B-33129D142612}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/
DefaultDirName={autopf}\Reqwa
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#SourcePath}\output
OutputBaseFilename=Reqwa-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName=Reqwa {#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Explicit group path (not {group}): upgrades remember the previous install's
; group name, which would keep creating icons in the old "Reqwa Colors" folder.
Name: "{autoprograms}\Reqwa\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  OldGroup, OldDesktop: String;
begin
  // Upgrades from "Reqwa Colors": drop the old Start Menu group / desktop
  // icon so they don't linger next to the new "Reqwa" ones.
  if CurStep = ssInstall then
  begin
    OldGroup := ExpandConstant('{autoprograms}\Reqwa Colors');
    if DirExists(OldGroup) then
      DelTree(OldGroup, True, True, True);
    OldDesktop := ExpandConstant('{autodesktop}\Reqwa Colors.lnk');
    if FileExists(OldDesktop) then
      DeleteFile(OldDesktop);
  end;
end;
