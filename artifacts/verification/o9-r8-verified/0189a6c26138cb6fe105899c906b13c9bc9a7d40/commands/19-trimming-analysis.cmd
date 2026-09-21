@echo off
setlocal
for %%I in ("%~dp0..\..\..\..\..") do set "REPO_ROOT=%%~fI"
for %%I in ("%~dp0..") do set "EVIDENCE_ROOT=%%~fI"
cd /d "%REPO_ROOT%"
dotnet build src/Convergence.Framework/Convergence.Framework.csproj --configuration Release --no-restore --no-incremental -p:EnableTrimAnalyzer=true -p:IsTrimmable=true -p:TreatWarningsAsErrors=true /clp:Summary > "%~dp019-trimming-analysis.raw.txt" 2>&1
set "COMMAND_EXIT=%ERRORLEVEL%"
exit /b %COMMAND_EXIT%
