@echo off
setlocal
for %%I in ("%~dp0..\..\..\..\..") do set "REPO_ROOT=%%~fI"
for %%I in ("%~dp0..") do set "EVIDENCE_ROOT=%%~fI"
cd /d "%REPO_ROOT%"
git diff --binary --full-index cc932dd375dd9a3d4e6f75d3755201119c4a350f..1345681594ee8d9bf34233877f7e73bc2745c9b8 > "%~dp022-reviewed-range-diff.raw.txt" 2>&1
set "COMMAND_EXIT=%ERRORLEVEL%"
exit /b %COMMAND_EXIT%
