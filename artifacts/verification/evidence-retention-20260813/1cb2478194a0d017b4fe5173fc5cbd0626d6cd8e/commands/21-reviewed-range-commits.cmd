@echo off
setlocal
for %%I in ("%~dp0..\..\..\..\..") do set "REPO_ROOT=%%~fI"
for %%I in ("%~dp0..") do set "EVIDENCE_ROOT=%%~fI"
cd /d "%REPO_ROOT%"
git log --format=fuller 96f58e47a77e31878bf89452bf7cad91cca5db55..1cb2478194a0d017b4fe5173fc5cbd0626d6cd8e > "%~dp021-reviewed-range-commits.raw.txt" 2>&1
set "COMMAND_EXIT=%ERRORLEVEL%"
exit /b %COMMAND_EXIT%
