; Kept to ASCII apart from translations: the custom messages below are UTF-8 and ISCC reads the
; file as UTF-8 because of its byte-order mark.
; The per-user installer: no UAC prompt, installs to %LocalAppData%\Programs\IDE-tabs, adds a
; Start menu shortcut and an Apps entry. build/Package-Release.ps1 compiles it:
;   ISCC.exe /DAppVersion=0.1.0-alpha.1 /DFileVersion=0.1.0 /DSourceExe=<exe> /DOutputDir=<dir> build\IDE-tabs.iss
#ifndef AppVersion
  #error Pass /DAppVersion=<version>
#endif
#ifndef FileVersion
  #define FileVersion "0.0.0"
#endif
#ifndef SourceExe
  #define SourceExe "..\WtProgram\bin\Release\IDE-tabs.exe"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

[Setup]
; Never change AppId: upgrades and the uninstaller find an earlier install by it.
AppId={{81C4FEE0-C952-4B76-AF84-674D6A9AEC5D}
AppName=IDE-tabs
AppVersion={#AppVersion}
AppVerName=IDE-tabs {#AppVersion}
AppPublisher=IDE-tabs contributors
AppPublisherURL=https://github.com/Pindaman77/IDE-tabs
AppSupportURL=https://github.com/Pindaman77/IDE-tabs/issues
AppUpdatesURL=https://github.com/Pindaman77/IDE-tabs/releases
VersionInfoVersion={#FileVersion}
VersionInfoProductTextVersion={#AppVersion}
; Per user: {autopf} is %LocalAppData%\Programs, and nothing asks for administrator rights.
PrivilegesRequired=lowest
DefaultDirName={autopf}\IDE-tabs
DisableDirPage=auto
DisableProgramGroupPage=yes
DisableReadyPage=yes
UsePreviousTasks=yes
MinVersion=10.0
SetupIconFile=..\WtProgram\Resources\IDE-tabs.ico
UninstallDisplayIcon={app}\IDE-tabs.exe
UninstallDisplayName=IDE-tabs
OutputDir={#OutputDir}
OutputBaseFilename=IDE-tabs-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; The running copy is asked to quit in PrepareToInstall; Restart Manager would close it less gently.
CloseApplications=no
RestartApplications=no
ShowLanguageDialog=auto
; With a certificate, Package-Release.ps1 passes /DSign and the sign tool as /Ssign=<command>,
; after signing the exe itself; the setup and its uninstaller are then signed as well.
#ifdef Sign
SignTool=sign
SignedUninstaller=yes
#endif

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "zh"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "ja"; MessagesFile: "compiler:Languages\Japanese.isl"

[CustomMessages]
en.StillRunning=IDE-tabs is still running. Quit it from its tray icon, then try again.
zh.StillRunning=IDE-tabs 仍在运行。请从托盘图标退出它，然后重试。
ja.StillRunning=IDE-tabs がまだ実行中です。トレイ アイコンから終了してから、もう一度お試しください。

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "IDE-tabs.exe"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; DestName: "THIRD_PARTY_NOTICES.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\IDE-tabs"; Filename: "{app}\IDE-tabs.exe"
Name: "{autodesktop}\IDE-tabs"; Filename: "{app}\IDE-tabs.exe"; Tasks: desktopicon

[Run]
; An update brings back the copy it quit, even when silent, so running the new setup is the
; whole update.
Filename: "{app}\IDE-tabs.exe"; Flags: nowait; Check: QuitForUpdate
Filename: "{app}\IDE-tabs.exe"; Description: "{cm:LaunchProgram,IDE-tabs}"; Flags: nowait postinstall skipifsilent; Check: not QuitForUpdate

[UninstallDelete]
; Created next to the exe at every start, as the folder is writable.
Type: files; Name: "{app}\IDE-tabs-crash.log"

[Code]
const
  EVENT_MODIFY_STATE = $0002;
  { Shared with Bootstrap.main in Program.fs. }
  RunningMutex = 'BemoSoftware.WindowTabs';
  ReplaceEvent = 'BemoSoftware.WindowTabs.Replace';
  RunKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run';

var
  WasRunning: Boolean;

function OpenEvent(Access: DWORD; Inherit: BOOL; Name: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(Event: THandle): BOOL;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): BOOL;
  external 'CloseHandle@kernel32.dll stdcall';

{ Asks a running copy to quit as from its tray menu, the way starting another copy does, so its
  exe can be replaced or removed. Editor windows stay open. }
function QuitRunningCopy(): Boolean;
var
  Event: THandle;
  Waited: Integer;
begin
  if CheckForMutexes(RunningMutex) then
  begin
    WasRunning := True;
    Event := OpenEvent(EVENT_MODIFY_STATE, False, ReplaceEvent);
    if Event <> 0 then
    begin
      SetEvent(Event);
      CloseHandle(Event);
    end;
    Waited := 0;
    while CheckForMutexes(RunningMutex) and (Waited < 15000) do
    begin
      Sleep(250);
      Waited := Waited + 250;
    end;
  end;
  Result := not CheckForMutexes(RunningMutex);
end;

function QuitForUpdate(): Boolean;
begin
  Result := WasRunning;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  if QuitRunningCopy() then
    Result := ''
  else
    Result := CustomMessage('StillRunning');
end;

function InitializeUninstall(): Boolean;
begin
  Result := QuitRunningCopy();
  if not Result then
    SuppressibleMsgBox(CustomMessage('StillRunning'), mbError, MB_OK, IDOK);
end;

{ IDE-tabs writes its own Start with Windows entry; remove it only when it names this copy. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Registered: String;
begin
  if (CurUninstallStep = usUninstall) and RegQueryStringValue(HKCU, RunKey, 'IDE-tabs', Registered) then
    if CompareText(RemoveQuotes(Trim(Registered)), ExpandConstant('{app}\IDE-tabs.exe')) = 0 then
      RegDeleteValue(HKCU, RunKey, 'IDE-tabs');
end;
