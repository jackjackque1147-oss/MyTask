#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

[Setup]
AppId={{F67F5E45-80B4-4D44-9B4A-A9B4D6B30E64}
AppName=MyTask
AppVersion={#MyAppVersion}
AppPublisher=jackjackque1147-oss
DefaultDirName={localappdata}\Programs\MyTask
DefaultGroupName=MyTask
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts
OutputBaseFilename=MyTask-Setup-v{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\MyTask.exe
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "..\publish\win-x64\MyTask.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\MyTask"; Filename: "{app}\MyTask.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\MyTask"; Filename: "{app}\MyTask.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\MyTask.exe"; Description: "Launch MyTask"; Flags: postinstall nowait skipifsilent
