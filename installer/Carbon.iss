; Carbon installer: one Carbon.exe, installed per user (no admin), registered as a Windows app and web browser.
; Re-running a newer setup updates the same Carbon.exe in place; browsing data in %LOCALAPPDATA%\Carbon is kept.
#ifndef AppExe
  #define AppExe "..\artifacts\Carbon\Carbon.exe"
#endif
#ifndef LauncherExe
  #define LauncherExe "..\artifacts\Carbon\CarbonOpen.exe"
#endif
#ifndef OutputRoot
  #define OutputRoot "..\artifacts"
#endif
#ifndef Bootstrapper
  #define Bootstrapper "..\tools\MicrosoftEdgeWebview2Setup.exe"
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{B8384B5A-3D3D-46D3-89AF-2D4984C9961A}
AppName=Carbon
AppVersion={#AppVersion}
AppVerName=Carbon {#AppVersion}
AppPublisher=Carbon
AppPublisherURL=https://github.com/K-I-X/Carbon-Browser
AppSupportURL=https://github.com/K-I-X/Carbon-Browser/issues
AppUpdatesURL=https://github.com/K-I-X/Carbon-Browser/releases
DefaultDirName={localappdata}\Programs\Carbon
DisableDirPage=auto
DefaultGroupName=Carbon
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputRoot}
OutputBaseFilename=Carbon-Setup-{#AppVersion}-x64
SetupIconFile=..\carbon\Assets\carbon.ico
UninstallDisplayIcon={app}\Carbon.exe
UninstallDisplayName=Carbon
LicenseFile=..\LICENSE
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
CloseApplicationsFilter=Carbon.exe
RestartApplications=yes
ChangesAssociations=yes
VersionInfoVersion={#AppVersion}.0
VersionInfoDescription=Carbon browser setup
VersionInfoProductName=Carbon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[InstallDelete]
; Older multi-file builds: remove their leftovers so only Carbon.exe remains.
Type: filesandordirs; Name: "{app}\*.dll"
Type: filesandordirs; Name: "{app}\Shell"
Type: filesandordirs; Name: "{app}\runtimes"
Type: filesandordirs; Name: "{app}\cs"
Type: filesandordirs; Name: "{app}\de"

[Files]
Source: "{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#LauncherExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Licenses\*"; DestDir: "{app}\Licenses"; Flags: ignoreversion recursesubdirs
Source: "{#Bootstrapper}"; Flags: dontcopy

[Icons]
Name: "{autoprograms}\Carbon"; Filename: "{app}\Carbon.exe"; AppUserModelID: "Carbon.Browser"
Name: "{userdesktop}\Carbon"; Filename: "{app}\Carbon.exe"; Tasks: desktopicon

[Registry]
; Web browser registration (per user). Windows asks the user to confirm Carbon as the default.
Root: HKCU; Subkey: "Software\Classes\CarbonURL"; ValueType: string; ValueData: "Carbon URL"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\CarbonURL"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\CarbonURL\DefaultIcon"; ValueType: string; ValueData: """{app}\Carbon.exe"",0"
Root: HKCU; Subkey: "Software\Classes\CarbonURL\shell\open\command"; ValueType: string; ValueData: """{app}\CarbonOpen.exe"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\CarbonHTML"; ValueType: string; ValueData: "Carbon HTML Document"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\CarbonHTML\DefaultIcon"; ValueType: string; ValueData: """{app}\Carbon.exe"",0"
Root: HKCU; Subkey: "Software\Classes\CarbonHTML\shell\open\command"; ValueType: string; ValueData: """{app}\CarbonOpen.exe"" ""%1"""
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\Carbon"; ValueType: string; ValueData: "Carbon"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\Carbon\DefaultIcon"; ValueType: string; ValueData: """{app}\Carbon.exe"",0"
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\Carbon\shell\open\command"; ValueType: string; ValueData: """{app}\Carbon.exe"""
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\Carbon\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "Carbon"
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\Carbon\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "The open-source agent browser that works with the AI apps on your computer."
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\Carbon\Capabilities"; ValueType: string; ValueName: "ApplicationIcon"; ValueData: """{app}\Carbon.exe"",0"
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\Carbon\Capabilities\StartMenu"; ValueType: string; ValueName: "StartMenuInternet"; ValueData: "Carbon"
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\Carbon\Capabilities\URLAssociations"; ValueType: string; ValueName: "http"; ValueData: "CarbonURL"
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\Carbon\Capabilities\URLAssociations"; ValueType: string; ValueName: "https"; ValueData: "CarbonURL"
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\Carbon\Capabilities\FileAssociations"; ValueType: string; ValueName: ".html"; ValueData: "CarbonHTML"
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\Carbon\Capabilities\FileAssociations"; ValueType: string; ValueName: ".htm"; ValueData: "CarbonHTML"
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\Carbon\Capabilities\FileAssociations"; ValueType: string; ValueName: ".pdf"; ValueData: "CarbonHTML"
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "Carbon"; ValueData: "Software\Clients\StartMenuInternet\Carbon\Capabilities"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\Carbon.exe"; Description: "Open Carbon"; Flags: nowait postinstall skipifsilent
Filename: "ms-settings:defaultapps?registeredAppUser=Carbon"; Description: "Make Carbon my default browser"; Flags: shellexec postinstall skipifsilent unchecked nowait

[Code]
const
  RuntimeKey = 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
function RuntimeInstalled: Boolean;
var Version: String;
begin
  Result := RegQueryStringValue(HKLM32, RuntimeKey, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0');
  if not Result then Result := RegQueryStringValue(HKCU, RuntimeKey, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var ExitCode: Integer;
begin
  Result := '';
  if RuntimeInstalled then exit;
  if WizardSilent then begin
    Result := 'Install Microsoft Edge WebView2 Runtime, then run Carbon Setup again.';
    exit;
  end;
  if MsgBox('Carbon needs Microsoft Edge WebView2 Runtime. Download and install it from Microsoft now? An internet connection is required.', mbConfirmation, MB_YESNO) <> IDYES then begin
    Result := 'WebView2 installation was cancelled. Nothing was changed.';
    exit;
  end;
  ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
  if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
    Result := 'Could not start the Microsoft runtime installer.'
  else if not RuntimeInstalled then
    Result := 'WebView2 Runtime could not be installed. Install it from Microsoft and retry Carbon Setup.';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Value: String;
begin
  if CurUninstallStep = usUninstall then begin
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Carbon', Value) and
      (Value = '"' + ExpandConstant('{app}\Carbon.exe') + '" --startup') then
      RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Carbon');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Applications\Carbon.exe');
    // Browser data (%LOCALAPPDATA%\Carbon) and the shared WebView2 runtime are deliberately kept.
  end;
end;
