@echo off
setlocal
for %%I in ("%~dp0..\..\..\..\..") do set "REPO_ROOT=%%~fI"
for %%I in ("%~dp0..") do set "EVIDENCE_ROOT=%%~fI"
cd /d "%REPO_ROOT%"
dotnet test tests/Convergence.Framework.Tests/Convergence.Framework.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~EquipmentInstanceOwnershipTests|FullyQualifiedName~EquipmentSlotLayoutTests|FullyQualifiedName~ResourceManagementServiceTests|FullyQualifiedName~ShopPricingPolicyTests|FullyQualifiedName~ShopStockPolicyTests|FullyQualifiedName~RecoveryPolicyTests|FullyQualifiedName~RuntimePersistenceSnapshotTests|FullyQualifiedName~RuntimeRulesetBindingTests|FullyQualifiedName~GodotIntegrationContractTests|FullyQualifiedName~DocumentationFoundationTests|FullyQualifiedName~DocumentationContractSynchronizationTests" --logger "console;verbosity=normal" > "%~dp005-focused-framework-tests.raw.txt" 2>&1
set "COMMAND_EXIT=%ERRORLEVEL%"
exit /b %COMMAND_EXIT%
