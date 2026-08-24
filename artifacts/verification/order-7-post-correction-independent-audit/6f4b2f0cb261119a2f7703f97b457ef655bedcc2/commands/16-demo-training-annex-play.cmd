@echo off
setlocal
for %%I in ("%~dp0..\..\..\..\..") do set "REPO_ROOT=%%~fI"
for %%I in ("%~dp0..") do set "EVIDENCE_ROOT=%%~fI"
cd /d "%REPO_ROOT%"
echo 10| dotnet run --project samples/Convergence.DemoHost/Convergence.DemoHost.csproj --configuration Release --no-build --no-restore -- --clean-training-annex-play > "%~dp016-demo-training-annex-play.raw.txt" 2>&1
set "COMMAND_EXIT=%ERRORLEVEL%"
exit /b %COMMAND_EXIT%
