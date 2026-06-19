; Inno Setup script for Unclock
; Download Inno Setup: https://jrsoftware.org/isdl.php
; Build: iscc setup.iss

#define MyAppName "Unclock"
#define MyAppVersion "1.0.0"
#define MyAppExeName "Unclock.exe"
#define ReleaseDir "..\PowerSaver\PowerSaver\bin\Release\net8.0-windows"

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Unclock
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=Output
OutputBaseFilename=Unclock-{#MyAppVersion}-setup
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\app.ico
DisableProgramGroupPage=yes
PrivilegesRequired=lowest

[Files]
Source: "{#ReleaseDir}\Unclock.exe"; DestDir: "{app}"
Source: "{#ReleaseDir}\Unclock.dll"; DestDir: "{app}"
Source: "{#ReleaseDir}\Unclock.deps.json"; DestDir: "{app}"
Source: "{#ReleaseDir}\Unclock.runtimeconfig.json"; DestDir: "{app}"
Source: "{#ReleaseDir}\Hardcodet.NotifyIcon.Wpf.dll"; DestDir: "{app}"
Source: "{#ReleaseDir}\System.Drawing.Common.dll"; DestDir: "{app}"
Source: "{#ReleaseDir}\app.ico"; DestDir: "{app}"
Source: "{#ReleaseDir}\amd_bridge.exe"; DestDir: "{app}"; Flags: skipifsourcedoesntexist
Source: "{#ReleaseDir}\ADLX.dll"; DestDir: "{app}"; Flags: skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Run Unclock"; Flags: postinstall nowait skipifsilent
