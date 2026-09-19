@echo off
setlocal
for %%I in ("%~dp0..\..\..\..\..") do set "REPO_ROOT=%%~fI"
for %%I in ("%~dp0..") do set "EVIDENCE_ROOT=%%~fI"
cd /d "%REPO_ROOT%"
git diff --binary --full-index a1f91e68dea9011d7d3493d76a071d6449e2dcd0..fd0ef3344c966505dd40a7700a2543ca42f533ba > "%~dp022-reviewed-range-diff.raw.txt" 2>&1
set "COMMAND_EXIT=%ERRORLEVEL%"
exit /b %COMMAND_EXIT%
