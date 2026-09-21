@echo off
setlocal
for %%I in ("%~dp0..\..\..\..\..") do set "REPO_ROOT=%%~fI"
for %%I in ("%~dp0..") do set "EVIDENCE_ROOT=%%~fI"
cd /d "%REPO_ROOT%"
git log --format=fuller d5f8845d86d5cca91adc3453d41f0fd3ca1dc6ab..0189a6c26138cb6fe105899c906b13c9bc9a7d40 > "%~dp021-reviewed-range-commits.raw.txt" 2>&1
set "COMMAND_EXIT=%ERRORLEVEL%"
exit /b %COMMAND_EXIT%
