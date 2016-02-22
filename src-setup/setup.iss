#define AppGuid "{@APP_GUID@"
#define AppName "LessUACRunner"
#define AppVersion "@APP_VERSION@.@APP_RELEASE@"
#define AppPublisher "@APP_AUTHOR@"
#define AppURL "@APP_URL@"
#define AppConsoleExe "LessUACRunnerConsole.exe"
#define AppServiceExe "LessUACRunnerService.exe"
#define AppServiceName = "LessUACRunnerService"
#define AppConsoleAdmin "LessUACRunnerAdmin.bat"
#define AppConsoleUser "LessUACRunnerUser.bat"

[Setup]
AppId={#AppGuid}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}

WizardSmallImageFile=setup-mini.bmp

DefaultDirName={sd}\{#AppName}
DisableDirPage=no
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
SetupLogging=yes
ChangesEnvironment=yes

OutputBaseFilename={#AppName}-{#AppVersion}
Compression=lzma2
SolidCompression=yes

PrivilegesRequired=admin
LicenseFile=license.txt

SetupIconFile={#AppName}.ico
UninstallDisplayName={#AppName} {#AppVersion}
UninstallDisplayIcon={#AppName}.ico

VersionInfoCopyright=Copyright © {#AppPublisher} 2016
VersionInfoCompany={#AppPublisher}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Dirs]
Name: "{app}\logs"
Name: "{app}\dist"

[Files]
Source: "{#AppName}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{commondesktop}\LessUACRunner Console"; Filename: "{app}\{#AppConsoleExe}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppName}.ico"

[Run]
Filename: "{app}\dist\dotnetfx35setup.exe"; Description: "Install .NET Framework 3.5"; Parameters: "/passive /norestart"; WorkingDir: "{app}"; Flags: postinstall waituntilterminated 32bit runhidden runascurrentuser
Filename: "{app}\{#AppConsoleExe}"; Description: "Install and start service"; Parameters: "-install"; WorkingDir: "{app}"; Flags: postinstall waituntilterminated 32bit runhidden runascurrentuser
Filename: "{app}\{#AppConsoleExe}"; Description: "Encrypt app settings"; Parameters: "-encrypt"; WorkingDir: "{app}"; Flags: postinstall waituntilterminated 32bit runhidden runascurrentuser

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop {#AppServiceName}"; WorkingDir: "{sys}"; Flags: waituntilterminated 32bit runhidden
Filename: "sc.exe"; Parameters: "delete {#AppServiceName}"; WorkingDir: "{sys}"; Flags: waituntilterminated 32bit runhidden
    
[UninstallDelete]
Type: filesandordirs; Name: "{app}\logs"
Type: filesandordirs; Name: "{app}\dist"

[Icons]
Name: "{commondesktop}\LessUACRunner (Admin)"; Filename: "{app}\{#AppConsoleAdmin}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppName}.ico"
Name: "{commondesktop}\LessUACRunner (User)"; Filename: "{app}\{#AppConsoleUser}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppName}.ico"

