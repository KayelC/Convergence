@echo off
setlocal
for %%I in ("%~dp0..\..\..\..\..") do set "REPO_ROOT=%%~fI"
for %%I in ("%~dp0..") do set "EVIDENCE_ROOT=%%~fI"
cd /d "%REPO_ROOT%"
dotnet run --project tools/Convergence.ContentValidator/Convergence.ContentValidator.csproj --configuration Release --no-build --no-restore -- --content-root content --schema-root schemas/content/v10 --registrations config/content-validator/active-samples.registrations.json > "%~dp011-content-validation.raw.txt" 2>&1
set "COMMAND_EXIT=%ERRORLEVEL%"
exit /b %COMMAND_EXIT%
