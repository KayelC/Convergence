@echo off
setlocal
for %%I in ("%~dp0..\..\..\..\..") do set "REPO_ROOT=%%~fI"
for %%I in ("%~dp0..") do set "EVIDENCE_ROOT=%%~fI"
cd /d "%REPO_ROOT%"
dotnet test tests/Convergence.DemoHost.Tests/Convergence.DemoHost.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CleanSaveDemoHostTests|FullyQualifiedName~CleanTrainingAnnexDemoHostTests|FullyQualifiedName~CleanTrainingAnnexPlayHostTests" --logger "console;verbosity=normal" > "%~dp006-focused-demohost-tests.raw.txt" 2>&1
set "COMMAND_EXIT=%ERRORLEVEL%"
exit /b %COMMAND_EXIT%
