#define MyAppName "HNL Tool - VXT Pro"
#define MyAppVersion "7.0.0-alpha.4"
#define MyNumericVersion "7.0.0.4"
#define MyPublisher "HNL Tool"
#define MySetupBaseName "HNL_VXT_Pro_Setup_7.0.0-alpha.4"

[Setup]
AppId={{A71F4558-7412-4B35-9EB8-6A2E2F2F6D44}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyPublisher}
VersionInfoVersion={#MyNumericVersion}
VersionInfoCompany={#MyPublisher}
VersionInfoDescription=HNL Tool - VXT Pro AutoCAD 2023 Installer
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyNumericVersion}
DefaultDirName={commonpf}\Autodesk\ApplicationPlugins\HNL.VXT.bundle
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\artifacts\installer
OutputBaseFilename={#MySetupBaseName}
SetupIconFile=..\artifacts\installer-assets\HNL-VXT.ico
WizardSmallImageFile=..\artifacts\installer-assets\HNL-VXT-Small.bmp
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
UninstallDisplayName={#MyAppName} v{#MyAppVersion}
UninstallDisplayIcon={app}\Assets\HNL-VXT.ico
CreateUninstallRegKey=yes
CloseApplications=no
RestartApplications=no
SetupLogging=yes

[InstallDelete]
Type: filesandordirs; Name: "{app}\Contents"
Type: filesandordirs; Name: "{app}\Assets"
Type: files; Name: "{app}\PackageContents.xml"

[Dirs]
Name: "{app}\Contents\Windows"
Name: "{app}\Contents\Legacy"
Name: "{app}\Assets"

[Files]
Source: "..\artifacts\HNL.VXT.bundle\PackageContents.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\HNL.VXT.bundle\Contents\Windows\*.dll"; DestDir: "{app}\Contents\Windows"; Flags: ignoreversion
Source: "..\artifacts\HNL.VXT.bundle\Contents\Windows\*.config"; DestDir: "{app}\Contents\Windows"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\legacy\HNL-VXT-V6.7.4-Golden.lsp"; DestDir: "{app}\Contents\Legacy"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\artifacts\installer-assets\HNL-VXT.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion

[Code]
function IsAutoCADRunning: Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  if Exec(ExpandConstant('{cmd}'), '/C tasklist /FI "IMAGENAME eq acad.exe" | find /I "acad.exe" >nul', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Result := (ResultCode = 0);
end;

function AutoCAD2023Detected: Boolean;
begin
  Result :=
    RegKeyExists(HKLM64, 'SOFTWARE\Autodesk\AutoCAD\R24.2') or
    RegKeyExists(HKCU64, 'SOFTWARE\Autodesk\AutoCAD\R24.2');
end;

function InitializeSetup: Boolean;
begin
  Result := False;

  if IsAutoCADRunning then
  begin
    MsgBox(
      'HNL Tool - VXT Pro' + #13#10 + #13#10 +
      'AutoCAD đang mở. Hãy đóng toàn bộ AutoCAD trước khi cài hoặc cập nhật VXT Pro.',
      mbError, MB_OK);
    Exit;
  end;

  if not AutoCAD2023Detected then
  begin
    if MsgBox(
      'HNL Tool không phát hiện AutoCAD 2023 (R24.2) trong Registry.' + #13#10 + #13#10 +
      'Bạn vẫn có thể cài trước plugin. Tiếp tục cài đặt?',
      mbConfirmation, MB_YESNO) <> IDYES then
      Exit;
  end;

  Result := True;
end;

function InitializeUninstall: Boolean;
begin
  Result := not IsAutoCADRunning;
  if not Result then
    MsgBox(
      'HNL Tool - VXT Pro' + #13#10 + #13#10 +
      'Hãy đóng AutoCAD trước khi gỡ VXT Pro.',
      mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    MsgBox(
      'Cài đặt HNL Tool - VXT Pro v{#MyAppVersion} hoàn tất.' + #13#10 + #13#10 +
      'Mở AutoCAD 2023 và gõ VXT để mở giao diện HNL VXT Pro.',
      mbInformation, MB_OK);
  end;
end;
