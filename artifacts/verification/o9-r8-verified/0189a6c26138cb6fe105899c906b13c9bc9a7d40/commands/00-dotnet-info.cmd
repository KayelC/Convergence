@echo off
setlocal
for %%I in ("%~dp0..\..\..\..\..") do set "REPO_ROOT=%%~fI"
for %%I in ("%~dp0..") do set "EVIDENCE_ROOT=%%~fI"
cd /d "%REPO_ROOT%"
dotnet --info > "%~dp000-dotnet-info.raw.txt" 2>&1
set "COMMAND_EXIT=%ERRORLEVEL%"
exit /b %COMMAND_EXIT%
