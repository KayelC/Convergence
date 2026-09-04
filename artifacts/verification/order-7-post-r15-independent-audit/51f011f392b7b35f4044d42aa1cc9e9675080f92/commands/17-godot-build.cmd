@echo off
setlocal
for %%I in ("%~dp0..\..\..\..\..") do set "REPO_ROOT=%%~fI"
for %%I in ("%~dp0..") do set "EVIDENCE_ROOT=%%~fI"
cd /d "%REPO_ROOT%"
dotnet build samples/Convergence.GodotHost/Convergence.GodotHost.csproj --configuration Debug --no-restore --no-incremental -warnaserror /clp:Summary > "%~dp017-godot-build.raw.txt" 2>&1
set "COMMAND_EXIT=%ERRORLEVEL%"
exit /b %COMMAND_EXIT%
