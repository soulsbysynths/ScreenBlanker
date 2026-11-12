; Inno Setup script for the Windows build of ScreenBlanker.
; Built by the Unity menu item ScreenBlanker > Build Windows Installer, or by hand:
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DAppVersion=1.0.0 Installer\ScreenBlanker.iss
; Expects the player build in Builds\Windows (ScreenBlanker > Build Windows).

#ifndef AppVersion
	#define AppVersion "1.0.0"
#endif

#define AppName "ScreenBlanker"
#define AppExe "ScreenBlanker.exe"
#define AppPublisher "Soulsby Synths"
#define BuildDir "..\Builds\Windows"

[Setup]
; Never change AppId: it is how upgrades and the uninstaller find an existing install.
AppId={{6B1E4C2D-8F3A-4D7B-9E21-5C0A7F4B3D19}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
; Per-user install: no admin prompt, and start-at-login is a per-user setting anyway.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
SetupIconFile=SetupIcon.ico
OutputDir=..\Builds\Installer
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; The app holds this mutex while running: setup and uninstall ask the user to quit it first,
; which removes the tray icon cleanly (killing the process would leave a ghost icon).
AppMutex=Local\SoulsbySynths.ScreenBlanker.SingleInstance
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#BuildDir}\*"; DestDir: "{app}"; Excludes: "*_BurstDebugInformation_DoNotShip,*_BackUpThisFolder_ButDontShipItWithYourGame"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; The app writes its own start-at-login entry; make sure uninstalling removes it.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "{#AppName}"; Flags: uninsdeletevalue dontcreatekey

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
