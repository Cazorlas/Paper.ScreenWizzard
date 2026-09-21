; The Paper.ScreenWizzard installer (docs/features/release/SPEC.md). Built by installer/build-package.ps1, which passes:
;   Version      x.y.z, from the release tag
;   PublishDir   the folder `dotnet publish` wrote (one self-contained exe)
;   IconFile     src/Paper.ScreenWizzard.App/app.ico
;   LicenseFile  LICENSE
;   OutDir       where Setup.exe is written
;
; Per user, not per machine: no administrator rights, files under %LocalAppData%\Programs, the shortcut in the user's Start menu,
; the entry in Apps under the user's own registry. The "run at logon" value is written by the app when the user turns it on in
; Settings; this installer only takes it away on uninstall. The settings and pictures under %AppData%\Paper\ScreenWizzard belong
; to the user and are never touched.
;
; Two languages: English and Vietnamese, chosen from the Windows display language (a choice box appears only when Windows is in
; neither). The Vietnamese messages are the community translation in Languages\Vietnamese.isl (from Inno Setup's own repository).

#ifndef Version
  #error Version is not defined (pass /DVersion=x.y.z)
#endif
#define AppName "Paper.ScreenWizzard"
#define AppExe "Paper.ScreenWizzard.exe"

[Setup]
; The id is what makes a newer Setup replace an older install: never change it.
AppId={{5C0E8A3B-7D14-4E6F-A2B9-3F8D1C6A9E40}
AppName={#AppName}
AppVersion={#Version}
AppVerName={#AppName} {#Version}
AppPublisher=Paper
AppPublisherURL=https://github.com/Cazorlas/Paper.ScreenWizzard
AppSupportURL=https://github.com/Cazorlas/Paper.ScreenWizzard/issues
VersionInfoVersion={#Version}
VersionInfoProductName={#AppName}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
; F1: Windows 10 version 1903 is build 18362; older Windows gets Inno's own message in its language.
MinVersion=10.0.18362
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutDir}
OutputBaseFilename=Paper.ScreenWizzard-{#Version}-win-x64-Setup
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\{#AppExe}
LicenseFile={#LicenseFile}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
WizardSmallImageFile=wizard-small.bmp
ShowLanguageDialog=auto
UsePreviousLanguage=no
; The app is a tray icon with no window, so it is ended by the code below, not asked to close.
CloseApplications=no
RestartApplications=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "vi"; MessagesFile: "Languages\Vietnamese.isl"

[CustomMessages]
en.NewerInstalled=A newer version of {#AppName} (%1) is already installed. Nothing was changed.
vi.NewerInstalled=Đã có phiên bản {#AppName} mới hơn (%1). Không có gì bị thay đổi.
en.RunNow=Run {#AppName} now
vi.RunNow=Chạy {#AppName} ngay

[Files]
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:RunNow}"; Flags: nowait postinstall skipifsilent

[Code]
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{5C0E8A3B-7D14-4E6F-A2B9-3F8D1C6A9E40}_is1';
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

// The first three numbers of x.y.z as one comparable number.
function VersionNumber(const Version: String): Int64;
var
  Rest, Part: String;
  P, I: Integer;
begin
  Result := 0;
  Rest := Version;
  for I := 1 to 3 do
  begin
    P := Pos('.', Rest);
    if P = 0 then
    begin
      Part := Rest;
      Rest := '';
    end
    else
    begin
      Part := Copy(Rest, 1, P - 1);
      Rest := Copy(Rest, P + 1, Length(Rest));
    end;
    Result := Result * 100000 + StrToIntDef(Part, 0);
  end;
end;

// F2: an older Setup over a newer install stops here and changes nothing.
function InitializeSetup(): Boolean;
var
  Installed: String;
begin
  Result := True;
  if RegQueryStringValue(HKCU, UninstallKey, 'DisplayVersion', Installed) then
    if VersionNumber(Installed) > VersionNumber('{#Version}') then
    begin
      MsgBox(FmtMessage(CustomMessage('NewerInstalled'), [Installed]), mbInformation, MB_OK);
      Result := False;
    end;
end;

// F4: the running app is ended, so the files can be replaced or removed and no restart of Windows is asked.
procedure StopTheApp();
var
  Code: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    StopTheApp();
end;

function InitializeUninstall(): Boolean;
begin
  StopTheApp();
  Result := True;
end;

// The "start with Windows" value belongs to the app; with the app gone it would point at nothing.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKCU, RunKey, '{#AppName}');
end;
