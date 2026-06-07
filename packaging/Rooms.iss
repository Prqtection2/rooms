; Inno Setup script for Rooms (M10).
; Build the payload first (self-contained, no .NET prerequisite on the target):
;   dotnet publish ..\src\Rooms.App -c Release -r win-x64 --self-contained true
; Then compile this script with the Inno Setup compiler (ISCC.exe) to produce RoomsSetup.exe.

#define AppName "Rooms"
#define AppVersion "1.0.0"
#define Publisher "Rooms contributors"
#define ExeName "Rooms.exe"
#define PublishDir "..\src\Rooms.App\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{B2B7E7E2-9C4D-4F4A-9D2E-ROOMSAPP0001}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputBaseFilename=RoomsSetup
OutputDir=dist
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Per-user install: no admin needed (hosts-file blocking, Option B, still requires elevation at runtime).
PrivilegesRequired=lowest
SetupIconFile=..\src\Rooms.App\Rooms.ico
UninstallDisplayIcon={app}\{#ExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Start Rooms automatically when I sign in"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#ExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#ExeName}"; Tasks: startup

[Run]
Filename: "{app}\{#ExeName}"; Description: "Launch Rooms now"; Flags: nowait postinstall skipifsilent
