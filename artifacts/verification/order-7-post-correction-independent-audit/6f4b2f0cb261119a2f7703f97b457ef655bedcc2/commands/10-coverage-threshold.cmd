@echo off
setlocal
for %%I in ("%~dp0..\..\..\..\..") do set "REPO_ROOT=%%~fI"
for %%I in ("%~dp0..") do set "EVIDENCE_ROOT=%%~fI"
cd /d "%REPO_ROOT%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\Assert-CoberturaCoverage.ps1 -CoveragePath "%EVIDENCE_ROOT%\coverage\coverage.cobertura.xml" -MinimumLineRate 0.90 -MinimumBranchRate 0.70 > "%~dp010-coverage-threshold.raw.txt" 2>&1
set "COMMAND_EXIT=%ERRORLEVEL%"
exit /b %COMMAND_EXIT%
