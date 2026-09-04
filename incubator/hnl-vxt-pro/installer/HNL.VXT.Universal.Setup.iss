#define MyAppName "HNL Tool - VXT Pro Universal"
#define MyAppVersion "7.0.0-alpha.3"
#define MyNumericVersion "7.0.0.3"
#define MyPublisher "HNL Tool"
#define MySetupBaseName "HNL_VXT_Pro_Universal_Setup_7.0.0-alpha.3"

[Setup]
AppId={{A71F4558-7412-4B35-9EB8-6A2E2F2F6D44}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyPublisher}
VersionInfoVersion={#MyNumericVersion}
VersionInfoCompany={#MyPublisher}
VersionInfoDescription=HNL Tool - VXT Pro Universal AutoCAD 2023-2027 Installer
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyNumericVersion}
DefaultDirName={commonpf}\Autodesk\ApplicationPlugins\HNL.VXT.bundle
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\artifacts\universal-installer
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
Name: "{app}\Contents\Windows\2023"
Name: "{app}\Contents\Windows\2024"
Name: "{app}\Contents\Windows\2025"
Name: "{app}\Contents\Windows\2026"
Name: "{app}\Contents\Windows\2027"
Name: "{app}\Assets"

[Files]
Source: "..\build\universal\PackageContents.2026-net8.xml"; DestDir: "{app}"; DestName: "PackageContents.xml"; Flags: ignoreversion; Check: not UseCad2026Net10
Source: "..\build\universal\PackageContents.2026-net10.xml"; DestDir: "{app}"; DestName: "PackageContents.xml"; Flags: ignoreversion; Check: UseCad2026Net10
Source: "..\artifacts\universal\2023\*"; DestDir: "{app}\Contents\Windows\2023"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\artifacts\universal\2024\*"; DestDir: "{app}\Contents\Windows\2024"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\artifacts\universal\2025\*"; DestDir: "{app}\Contents\Windows\2025"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\artifacts\universal\2026-net8\*"; DestDir: "{app}\Contents\Windows\2026"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: not UseCad2026Net10
Source: "..\artifacts\universal\2026-net10\*"; DestDir: "{app}\Contents\Windows\2026"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: UseCad2026Net10
Source: "..\artifacts\universal\2027\*"; DestDir: "{app}\Contents\Windows\2027"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\artifacts\installer-assets\HNL-VXT.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion

[Code]
var
  Cad2026Net10Cached: Boolean;
  Cad2026ChoiceResolved: Boolean;
  DetectedCadSummary: String;

function IsAutoCADRunning: Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  if Exec(ExpandConstant('{cmd}'), '/C tasklist /FI "IMAGENAME eq acad.exe" | find /I "acad.exe" >nul', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Result := (ResultCode = 0);
end;

function AutoCADReleaseDetected(ReleaseKey: String): Boolean;
begin
  Result :=
    RegKeyExists(HKLM64, 'SOFTWARE\Autodesk\AutoCAD\' + ReleaseKey) or
    RegKeyExists(HKCU64, 'SOFTWARE\Autodesk\AutoCAD\' + ReleaseKey);
end;

function FindAcadExeFromRegistry(ReleaseKey: String; var ExePath: String): Boolean;
var
  BaseKey: String;
  Names: TArrayOfString;
  I: Integer;
  Location: String;
begin
  Result := False;
  ExePath := '';
  BaseKey := 'SOFTWARE\Autodesk\AutoCAD\' + ReleaseKey;

  if RegGetSubkeyNames(HKLM64, BaseKey, Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      if RegQueryStringValue(HKLM64, BaseKey + '\' + Names[I], 'AcadLocation', Location) then
      begin
        ExePath := AddBackslash(Location) + 'acad.exe';
        if FileExists(ExePath) then
        begin
          Result := True;
          Exit;
        end;
      end;
    end;
  end;

  if RegGetSubkeyNames(HKCU64, BaseKey, Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      if RegQueryStringValue(HKCU64, BaseKey + '\' + Names[I], 'AcadLocation', Location) then
      begin
        ExePath := AddBackslash(Location) + 'acad.exe';
        if FileExists(ExePath) then
        begin
          Result := True;
          Exit;
        end;
      end;
    end;
  end;
end;

function ThirdVersionPart(VersionText: String): Integer;
var
  P1, P2, P3: Integer;
  Rest1, Rest2, Token: String;
begin
  Result := 0;
  P1 := Pos('.', VersionText);
  if P1 = 0 then Exit;
  Rest1 := Copy(VersionText, P1 + 1, Length(VersionText));
  P2 := Pos('.', Rest1);
  if P2 = 0 then Exit;
  Rest2 := Copy(Rest1, P2 + 1, Length(Rest1));
  P3 := Pos('.', Rest2);
  if P3 > 0 then
    Token := Copy(Rest2, 1, P3 - 1)
  else
    Token := Rest2;
  Result := StrToIntDef(Token, 0);
end;

function ResolveCad2026Net10: Boolean;
var
  ExePath, VersionText: String;
  BuildPart: Integer;
begin
  if Cad2026ChoiceResolved then
  begin
    Result := Cad2026Net10Cached;
    Exit;
  end;

  Cad2026ChoiceResolved := True;
  Cad2026Net10Cached := True;

  if not AutoCADReleaseDetected('R25.1') then
  begin
    Result := Cad2026Net10Cached;
    Exit;
  end;

  if not FindAcadExeFromRegistry('R25.1', ExePath) then
    ExePath := ExpandConstant('{commonpf}\Autodesk\AutoCAD 2026\acad.exe');

  if FileExists(ExePath) and GetVersionNumbersString(ExePath, VersionText) then
  begin
    BuildPart := ThirdVersionPart(VersionText);
    Cad2026Net10Cached := BuildPart >= 179;
    Log(Format('HNL VXT: AutoCAD 2026 acad.exe=%s, version=%s, buildPart=%d, NET10=%d', [ExePath, VersionText, BuildPart, Ord(Cad2026Net10Cached)]));
  end
  else
  begin
    Cad2026Net10Cached := True;
    Log('HNL VXT: Could not resolve AutoCAD 2026 executable version; defaulting to NET10 build.');
  end;

  Result := Cad2026Net10Cached;
end;

function UseCad2026Net10: Boolean;
begin
  Result := ResolveCad2026Net10;
end;

function BuildDetectedSummary: String;
begin
  Result := '';
  if AutoCADReleaseDetected('R24.2') then Result := Result + 'AutoCAD 2023' + #13#10;
  if AutoCADReleaseDetected('R24.3') then Result := Result + 'AutoCAD 2024' + #13#10;
  if AutoCADReleaseDetected('R25.0') then Result := Result + 'AutoCAD 2025' + #13#10;
  if AutoCADReleaseDetected('R25.1') then
  begin
    if ResolveCad2026Net10 then
      Result := Result + 'AutoCAD 2026 - NET10 (2026.1.2+)' + #13#10
    else
      Result := Result + 'AutoCAD 2026 - NET8 (trước 2026.1.2)' + #13#10;
  end;
  if AutoCADReleaseDetected('R26.0') then Result := Result + 'AutoCAD 2027' + #13#10;
end;

function InitializeSetup: Boolean;
begin
  Result := False;

  if IsAutoCADRunning then
  begin
    MsgBox(
      'HNL Tool - VXT Pro Universal' + #13#10 + #13#10 +
      'AutoCAD đang mở. Hãy đóng toàn bộ AutoCAD trước khi cài hoặc cập nhật VXT Pro.',
      mbError, MB_OK);
    Exit;
  end;

  DetectedCadSummary := BuildDetectedSummary;
  if DetectedCadSummary = '' then
  begin
    if MsgBox(
      'HNL Tool chưa phát hiện AutoCAD 2023-2027 trên máy.' + #13#10 + #13#10 +
      'Bạn vẫn có thể cài trước Universal Plugin. Tiếp tục?',
      mbConfirmation, MB_YESNO) <> IDYES then
      Exit;
  end
  else
  begin
    if MsgBox(
      'HNL Tool - VXT Pro Universal phát hiện:' + #13#10 + #13#10 +
      DetectedCadSummary + #13#10 +
      'Setup sẽ cài bộ plugin tương thích AutoCAD 2023-2027.' + #13#10 +
      'Tiếp tục cài đặt?',
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
      'HNL Tool - VXT Pro Universal' + #13#10 + #13#10 +
      'Hãy đóng AutoCAD trước khi gỡ VXT Pro.',
      mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    MsgBox(
      'Cài đặt HNL Tool - VXT Pro Universal v{#MyAppVersion} hoàn tất.' + #13#10 + #13#10 +
      'Hỗ trợ: AutoCAD 2023, 2024, 2025, 2026 và 2027.' + #13#10 +
      'Mở AutoCAD và gõ VXT để mở giao diện HNL VXT Pro.' + #13#10 + #13#10 +
      'Nếu AutoCAD 2026 được nâng từ trước 2026.1.2 lên 2026.1.2+, hãy chạy lại Setup để chuyển binary NET8 sang NET10.',
      mbInformation, MB_OK);
  end;
end;
