@ECHO OFF
TITLE LessUACRunner Admin
COLOR 47

REM BatchGotAdmin: https://searchcode.com/codesearch/view/45808546/  

REM Check for permissions  
>nul 2>&1 "%SYSTEMROOT%\system32\cacls.exe" "%SYSTEMROOT%\system32\config\system"  
  
IF '%ERRORLEVEL%' NEQ '0' (
    ECHO Requesting administrative privileges...
    GOTO UAC_PROMPT
) else (
    GOTO ADMIN
)
  
:UAC_PROMPT  
ECHO Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
ECHO UAC.ShellExecute "%~s0", "", "", "runas", 1 >> "%temp%\getadmin.vbs"
"%TEMP%\getadmin.vbs"
EXIT /B
  
:ADMIN
pause
IF EXIST "%temp%\getadmin.vbs" ( DEL "%temp%\getadmin.vbs" )
PUSHD "%CD%"
CD /D "%~dp0"
CD %CD%
%COMSPEC% /k "LessUACRunnerConsole.exe" -help
EXIT

:END
