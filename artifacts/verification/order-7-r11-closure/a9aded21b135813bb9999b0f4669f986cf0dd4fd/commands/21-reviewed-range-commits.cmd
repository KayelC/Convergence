@echo off
setlocal
for %%I in ("%~dp0..\..\..\..\..") do set "REPO_ROOT=%%~fI"
for %%I in ("%~dp0..") do set "EVIDENCE_ROOT=%%~fI"
cd /d "%REPO_ROOT%"
git log --format=fuller 25c0a78a23df1526ad53fbdbf151afd2efd693ad..a9aded21b135813bb9999b0f4669f986cf0dd4fd > "%~dp021-reviewed-range-commits.raw.txt" 2>&1
set "COMMAND_EXIT=%ERRORLEVEL%"
exit /b %COMMAND_EXIT%
