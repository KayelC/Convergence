@echo off
setlocal
for %%I in ("%~dp0..\..\..\..\..") do set "REPO_ROOT=%%~fI"
for %%I in ("%~dp0..") do set "EVIDENCE_ROOT=%%~fI"
cd /d "%REPO_ROOT%"
dotnet restore Convergence.sln --locked-mode -p:NuGetAudit=true -p:NuGetAuditMode=all "-p:WarningsAsErrors=NU1901%%3BNU1902%%3BNU1903%%3BNU1904" > "%~dp001-restore-audit.raw.txt" 2>&1
set "COMMAND_EXIT=%ERRORLEVEL%"
exit /b %COMMAND_EXIT%
