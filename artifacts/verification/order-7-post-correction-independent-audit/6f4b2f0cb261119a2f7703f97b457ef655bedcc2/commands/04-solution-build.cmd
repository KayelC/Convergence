@echo off
setlocal
for %%I in ("%~dp0..\..\..\..\..") do set "REPO_ROOT=%%~fI"
for %%I in ("%~dp0..") do set "EVIDENCE_ROOT=%%~fI"
cd /d "%REPO_ROOT%"
dotnet build Convergence.sln --configuration Release --no-restore --no-incremental -p:TreatWarningsAsErrors=true -p:ContinuousIntegrationBuild=true /clp:Summary > "%~dp004-solution-build.raw.txt" 2>&1
set "COMMAND_EXIT=%ERRORLEVEL%"
exit /b %COMMAND_EXIT%
