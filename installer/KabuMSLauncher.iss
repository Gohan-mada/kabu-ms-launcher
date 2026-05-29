; かぶ自動LOGIN（MarketspeedⅡ 自動起動アプリ） - Inno Setup script
; Build: ISCC.exe KabuMSLauncher.iss

#define MyAppName       "かぶ自動LOGIN"
#define MyAppShortName  "KabuMSLauncher"
#define MyAppVersion    "1.0.0"
#define MyAppPublisher  "株式会社ボストーク / かぶ自動化LAB"
#define MyAppURL        "https://cabu.info/"
#define MyAppExeName    "KabuMSLauncher.exe"
#define MyAppCopyright  "Copyright (C) 2026 Bostok Inc."

[Setup]
AppId={{D6E2A1F0-7E4C-4BCA-9B6A-2E15A0C2F8A4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright={#MyAppCopyright}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoDescription={#MyAppName}（MarketspeedⅡ 自動起動アプリ）Setup

; Per-user install — no admin required, plays nicely with SmartScreen
DefaultDirName={localappdata}\KabuMSLauncher
DefaultGroupName={#MyAppShortName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=no

; Output
OutputDir=..\dist
OutputBaseFilename=KabuMSLauncher_Setup_{#MyAppVersion}
Compression=lzma2/ultra
SolidCompression=yes

; Branding
WizardStyle=modern
SetupIconFile=..\KabuMSLauncher\Assets\app.ico
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "デスクトップにショートカットを作成"; GroupDescription: "追加アイコン:"; Flags: checkedonce
Name: "startupicon"; Description: "Windows起動時に自動で起動する"; GroupDescription: "自動起動:"; Flags: unchecked

[Files]
Source: "..\KabuMSLauncher\bin\Release\KabuMSLauncher.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme; DestName: "README.txt"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{#MyAppName} のアンインストール"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove credential store on uninstall (encrypted password + login id)
Type: filesandordirs; Name: "{localappdata}\KabuMSLauncher"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
