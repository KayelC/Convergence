[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z0-9][a-z0-9._-]*$')]
    [string]$Checkpoint,

    [string]$ReviewedBase,

    [string]$ReviewedHead,

    [Parameter(Mandatory = $true)]
    [string]$GodotExecutable
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-GitText {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }

    return @($output | ForEach-Object { $_.ToString() })
}

function Resolve-GitCommit {
    param([Parameter(Mandatory = $true)][string]$Revision)

    $output = @(Invoke-GitText -Arguments @('rev-parse', '--verify', "$Revision^{commit}"))
    return $output[0].Trim()
}

function Convert-ToRelativeEvidencePath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $rootUri = [Uri]((Resolve-Path -LiteralPath $Root).Path.TrimEnd('\') + '\')
    $pathUri = [Uri](Resolve-Path -LiteralPath $Path).Path
    return [Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString())
}

$repositoryOutput = @(Invoke-GitText -Arguments @('rev-parse', '--show-toplevel'))
$repositoryRoot = [IO.Path]::GetFullPath($repositoryOutput[0].Trim())
Set-Location -LiteralPath $repositoryRoot

$statusBefore = @(Invoke-GitText -Arguments @('status', '--porcelain=v1', '--untracked-files=all'))
if ($statusBefore.Count -ne 0) {
    throw "Verification evidence requires a clean worktree. Current entries:$([Environment]::NewLine)$($statusBefore -join [Environment]::NewLine)"
}

$testedCommit = Resolve-GitCommit -Revision 'HEAD'
$resolvedGodot = (Resolve-Path -LiteralPath $GodotExecutable -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $resolvedGodot -PathType Leaf)) {
    throw "Godot executable '$GodotExecutable' is not a file."
}
$repositoryPrefix = $repositoryRoot.TrimEnd('\') + '\'
if ($resolvedGodot.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    $godotDescriptor = Convert-ToRelativeEvidencePath -Root $repositoryRoot -Path $resolvedGodot
    $godotCommandPath = '"%REPO_ROOT%\' + $godotDescriptor.Replace('/', '\') + '"'
}
else {
    $godotDescriptor = [IO.Path]::GetFileName($resolvedGodot)
    $env:CONVERGENCE_GODOT_EXECUTABLE = $resolvedGodot
    $godotCommandPath = '"%CONVERGENCE_GODOT_EXECUTABLE%"'
}
$godotSha256 = (Get-FileHash -LiteralPath $resolvedGodot -Algorithm SHA256).Hash.ToLowerInvariant()

if (($ReviewedBase.Length -eq 0) -xor ($ReviewedHead.Length -eq 0)) {
    throw 'ReviewedBase and ReviewedHead must be supplied together.'
}

$resolvedBase = $null
$resolvedHead = $null
if ($ReviewedBase.Length -ne 0) {
    $resolvedBase = Resolve-GitCommit -Revision $ReviewedBase
    $resolvedHead = Resolve-GitCommit -Revision $ReviewedHead
}

$evidenceRoot = Join-Path $repositoryRoot "artifacts\verification\$Checkpoint\$testedCommit"
if (Test-Path -LiteralPath $evidenceRoot) {
    throw "Verification evidence destination already exists: $evidenceRoot"
}

$commandsRoot = Join-Path $evidenceRoot 'commands'
$coverageRoot = Join-Path $evidenceRoot 'coverage'
New-Item -ItemType Directory -Path $commandsRoot -Force | Out-Null
New-Item -ItemType Directory -Path $coverageRoot -Force | Out-Null

[IO.File]::WriteAllText(
    (Join-Path $evidenceRoot 'git-status-before.txt'),
    ($statusBefore -join [Environment]::NewLine),
    [Text.UTF8Encoding]::new($false))

$commandRecords = New-Object System.Collections.ArrayList
$startedUtc = [DateTime]::UtcNow
$runFailure = $null
$coverageRecord = $null

function Invoke-RecordedCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$CommandLine
    )

    $commandFile = Join-Path $commandsRoot "$Name.cmd"
    $outputFile = Join-Path $commandsRoot "$Name.raw.txt"
    $batch = @(
        '@echo off',
        'setlocal',
        'for %%I in ("%~dp0..\..\..\..\..") do set "REPO_ROOT=%%~fI"',
        'for %%I in ("%~dp0..") do set "EVIDENCE_ROOT=%%~fI"',
        'cd /d "%REPO_ROOT%"',
        "$CommandLine > `"%~dp0$Name.raw.txt`" 2>&1",
        'set "COMMAND_EXIT=%ERRORLEVEL%"',
        'exit /b %COMMAND_EXIT%'
    )
    [IO.File]::WriteAllLines($commandFile, $batch, [Text.Encoding]::ASCII)

    $commandStarted = [DateTime]::UtcNow
    & $env:ComSpec /d /c call "`"$commandFile`""
    $exitCode = $LASTEXITCODE
    $commandCompleted = [DateTime]::UtcNow

    if (Test-Path -LiteralPath $outputFile) {
        Get-Content -LiteralPath $outputFile | ForEach-Object { Write-Host $_ }
    }

    [void]$commandRecords.Add([ordered]@{
        order = $commandRecords.Count + 1
        name = $Name
        command = $CommandLine
        commandFile = "commands/$Name.cmd"
        outputFile = "commands/$Name.raw.txt"
        startedUtc = $commandStarted.ToString('o')
        completedUtc = $commandCompleted.ToString('o')
        exitCode = $exitCode
    })

    if ($exitCode -ne 0) {
        throw "Verification command '$Name' failed with exit code $exitCode."
    }
}

try {
    Invoke-RecordedCommand -Name '00-dotnet-info' -CommandLine 'dotnet --info'
    Invoke-RecordedCommand -Name '01-restore-audit' -CommandLine 'dotnet restore Convergence.sln --locked-mode -p:NuGetAudit=true -p:NuGetAuditMode=all "-p:WarningsAsErrors=NU1901%%3BNU1902%%3BNU1903%%3BNU1904"'
    Invoke-RecordedCommand -Name '02-format' -CommandLine 'dotnet format Convergence.sln --no-restore --verify-no-changes --verbosity diagnostic'
    Invoke-RecordedCommand -Name '03-framework-build' -CommandLine 'dotnet build src/Convergence.Framework/Convergence.Framework.csproj --configuration Release --no-restore --no-incremental -p:TreatWarningsAsErrors=true -p:ContinuousIntegrationBuild=true /clp:Summary'
    Invoke-RecordedCommand -Name '04-solution-build' -CommandLine 'dotnet build Convergence.sln --configuration Release --no-restore --no-incremental -p:TreatWarningsAsErrors=true -p:ContinuousIntegrationBuild=true /clp:Summary'

    $frameworkFilter = 'FullyQualifiedName~EquipmentInstanceOwnershipTests|FullyQualifiedName~EquipmentSlotLayoutTests|FullyQualifiedName~ResourceManagementServiceTests|FullyQualifiedName~ShopPricingPolicyTests|FullyQualifiedName~ShopStockPolicyTests|FullyQualifiedName~RecoveryPolicyTests|FullyQualifiedName~RuntimePersistenceSnapshotTests|FullyQualifiedName~RuntimeRulesetBindingTests|FullyQualifiedName~GodotIntegrationContractTests|FullyQualifiedName~DocumentationFoundationTests|FullyQualifiedName~DocumentationContractSynchronizationTests'
    Invoke-RecordedCommand -Name '05-focused-framework-tests' -CommandLine "dotnet test tests/Convergence.Framework.Tests/Convergence.Framework.Tests.csproj --configuration Release --no-build --no-restore --filter `"$frameworkFilter`" --logger `"console;verbosity=normal`""

    $demoFilter = 'FullyQualifiedName~CleanSaveDemoHostTests|FullyQualifiedName~CleanTrainingAnnexDemoHostTests|FullyQualifiedName~CleanTrainingAnnexPlayHostTests'
    Invoke-RecordedCommand -Name '06-focused-demohost-tests' -CommandLine "dotnet test tests/Convergence.DemoHost.Tests/Convergence.DemoHost.Tests.csproj --configuration Release --no-build --no-restore --filter `"$demoFilter`" --logger `"console;verbosity=normal`""

    Invoke-RecordedCommand -Name '07-architecture-tests' -CommandLine 'dotnet test tests/Convergence.Framework.Tests/Convergence.Framework.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~Convergence.Framework.Tests.Architecture" --logger "console;verbosity=normal"'
    Invoke-RecordedCommand -Name '08-full-tests' -CommandLine 'dotnet test Convergence.sln --configuration Release --no-build --no-restore --logger "console;verbosity=normal"'

    $coverageWork = Join-Path $evidenceRoot 'coverage-work'
    Invoke-RecordedCommand -Name '09-framework-coverage' -CommandLine 'dotnet test tests/Convergence.Framework.Tests/Convergence.Framework.Tests.csproj --configuration Release --no-build --no-restore --collect:"XPlat Code Coverage" --results-directory "%EVIDENCE_ROOT%\coverage-work" --logger "console;verbosity=minimal"'
    $coverageFiles = @(Get-ChildItem -LiteralPath $coverageWork -Recurse -Filter 'coverage.cobertura.xml' -File)
    if ($coverageFiles.Count -ne 1) {
        throw "Expected exactly one Cobertura report, found $($coverageFiles.Count)."
    }
    $normalizedCoverage = Join-Path $coverageRoot 'coverage.cobertura.xml'
    Move-Item -LiteralPath $coverageFiles[0].FullName -Destination $normalizedCoverage
    Remove-Item -LiteralPath $coverageWork -Recurse -Force

    Invoke-RecordedCommand -Name '10-coverage-threshold' -CommandLine 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\Assert-CoberturaCoverage.ps1 -CoveragePath "%EVIDENCE_ROOT%\coverage\coverage.cobertura.xml" -MinimumLineRate 0.90 -MinimumBranchRate 0.70'

    [xml]$coverageXml = Get-Content -LiteralPath $normalizedCoverage
    $coverageRecord = [ordered]@{
        compressedFile = 'coverage/coverage.cobertura.xml.gz'
        uncompressedSha256 = (Get-FileHash -LiteralPath $normalizedCoverage -Algorithm SHA256).Hash.ToLowerInvariant()
        lineRate = [decimal]$coverageXml.coverage.'line-rate'
        branchRate = [decimal]$coverageXml.coverage.'branch-rate'
        linesCovered = [int]$coverageXml.coverage.'lines-covered'
        linesValid = [int]$coverageXml.coverage.'lines-valid'
        branchesCovered = [int]$coverageXml.coverage.'branches-covered'
        branchesValid = [int]$coverageXml.coverage.'branches-valid'
    }

    Invoke-RecordedCommand -Name '11-content-validation' -CommandLine 'dotnet run --project tools/Convergence.ContentValidator/Convergence.ContentValidator.csproj --configuration Release --no-build --no-restore -- --content-root content --schema-root schemas/content/v10 --registrations config/content-validator/active-samples.registrations.json'
    Invoke-RecordedCommand -Name '12-demo-battle' -CommandLine 'dotnet run --project samples/Convergence.DemoHost/Convergence.DemoHost.csproj --configuration Release --no-build --no-restore -- --clean-battle-demo'
    Invoke-RecordedCommand -Name '13-demo-field' -CommandLine 'dotnet run --project samples/Convergence.DemoHost/Convergence.DemoHost.csproj --configuration Release --no-build --no-restore -- --clean-field-demo'
    Invoke-RecordedCommand -Name '14-demo-save' -CommandLine 'dotnet run --project samples/Convergence.DemoHost/Convergence.DemoHost.csproj --configuration Release --no-build --no-restore -- --clean-save-demo'
    Invoke-RecordedCommand -Name '15-demo-training-annex' -CommandLine 'dotnet run --project samples/Convergence.DemoHost/Convergence.DemoHost.csproj --configuration Release --no-build --no-restore -- --clean-training-annex-demo'
    Invoke-RecordedCommand -Name '16-demo-training-annex-play' -CommandLine 'echo 10| dotnet run --project samples/Convergence.DemoHost/Convergence.DemoHost.csproj --configuration Release --no-build --no-restore -- --clean-training-annex-play'
    Invoke-RecordedCommand -Name '17-godot-build' -CommandLine 'dotnet build samples/Convergence.GodotHost/Convergence.GodotHost.csproj --configuration Debug --no-restore --no-incremental -warnaserror /clp:Summary'
    Invoke-RecordedCommand -Name '18-godot-smoke' -CommandLine "$godotCommandPath --headless --path samples/Convergence.GodotHost -- --convergence-smoke"
    Invoke-RecordedCommand -Name '19-trimming-analysis' -CommandLine 'dotnet build src/Convergence.Framework/Convergence.Framework.csproj --configuration Release --no-restore --no-incremental -p:EnableTrimAnalyzer=true -p:IsTrimmable=true -p:TreatWarningsAsErrors=true /clp:Summary'
    Invoke-RecordedCommand -Name '20-diff-check' -CommandLine 'git diff --check'

    if ($null -ne $resolvedBase) {
        Invoke-RecordedCommand -Name '21-reviewed-range-commits' -CommandLine "git log --format=fuller $resolvedBase..$resolvedHead"
        Copy-Item -LiteralPath (Join-Path $commandsRoot '21-reviewed-range-commits.raw.txt') -Destination (Join-Path $evidenceRoot 'reviewed-range-commits.txt')
        Invoke-RecordedCommand -Name '22-reviewed-range-diff' -CommandLine "git diff --binary --full-index $resolvedBase..$resolvedHead"
        Copy-Item -LiteralPath (Join-Path $commandsRoot '22-reviewed-range-diff.raw.txt') -Destination (Join-Path $evidenceRoot 'reviewed-range.diff')
    }
}
catch {
    $runFailure = $_
}
finally {
    if (Test-Path -LiteralPath (Join-Path $coverageRoot 'coverage.cobertura.xml')) {
        $source = [IO.File]::OpenRead((Join-Path $coverageRoot 'coverage.cobertura.xml'))
        try {
            $destination = [IO.File]::Create((Join-Path $coverageRoot 'coverage.cobertura.xml.gz'))
            try {
                $gzip = [IO.Compression.GZipStream]::new(
                    $destination,
                    [IO.Compression.CompressionMode]::Compress,
                    $true)
                try {
                    $source.CopyTo($gzip)
                }
                finally {
                    $gzip.Dispose()
                }
            }
            finally {
                $destination.Dispose()
            }
        }
        finally {
            $source.Dispose()
        }
        Remove-Item -LiteralPath (Join-Path $coverageRoot 'coverage.cobertura.xml')
    }

    $statusAfter = @(Invoke-GitText -Arguments @('status', '--porcelain=v1', '--untracked-files=all'))
    [IO.File]::WriteAllText(
        (Join-Path $evidenceRoot 'git-status-after.txt'),
        ($statusAfter -join [Environment]::NewLine),
        [Text.UTF8Encoding]::new($false))

    $completedUtc = [DateTime]::UtcNow
    $manifest = [ordered]@{
        schemaVersion = 1
        checkpoint = $Checkpoint
        status = if ($null -eq $runFailure) { 'succeeded' } else { 'failed' }
        testedCommit = $testedCommit
        reviewedRange = if ($null -eq $resolvedBase) { $null } else { [ordered]@{ base = $resolvedBase; head = $resolvedHead } }
        repositoryWasClean = $statusBefore.Count -eq 0
        startedUtc = $startedUtc.ToString('o')
        completedUtc = $completedUtc.ToString('o')
        host = [ordered]@{
            operatingSystem = [Environment]::OSVersion.VersionString
            powershellVersion = $PSVersionTable.PSVersion.ToString()
            godotExecutable = $godotDescriptor
            godotSha256 = $godotSha256
        }
        commands = @($commandRecords)
        coverage = $coverageRecord
        failure = if ($null -eq $runFailure) { $null } else { $runFailure.Exception.Message }
    }
    [IO.File]::WriteAllText(
        (Join-Path $evidenceRoot 'manifest.json'),
        ($manifest | ConvertTo-Json -Depth 10),
        [Text.UTF8Encoding]::new($false))

    $readme = @(
        "# Verification Evidence: $Checkpoint",
        '',
        "- Tested commit: ``$testedCommit``",
        "- Status: ``$($manifest.status)``",
        "- Started UTC: ``$($manifest.startedUtc)``",
        "- Completed UTC: ``$($manifest.completedUtc)``",
        "- Commands recorded: $($commandRecords.Count)"
    )
    if ($null -ne $resolvedBase) {
        $readme += "- Reviewed range: ``$resolvedBase..$resolvedHead``"
    }
    $readme += @(
        '',
        'Raw command output is stored under `commands/`. Coverage is preserved',
        'losslessly as `coverage/coverage.cobertura.xml.gz`. Verify every file',
        'against `SHA256SUMS.txt` before relying on this bundle.'
    )
    [IO.File]::WriteAllLines(
        (Join-Path $evidenceRoot 'README.md'),
        $readme,
        [Text.UTF8Encoding]::new($false))

    $checksumLines = Get-ChildItem -LiteralPath $evidenceRoot -Recurse -File |
        Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
        ForEach-Object {
            $relative = Convert-ToRelativeEvidencePath -Root $evidenceRoot -Path $_.FullName
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            "$hash  $relative"
        } |
        Sort-Object
    [IO.File]::WriteAllLines(
        (Join-Path $evidenceRoot 'SHA256SUMS.txt'),
        $checksumLines,
        [Text.UTF8Encoding]::new($false))

    if ($null -ne $runFailure) {
        $failedCheckpoint = "$Checkpoint-failed-$($startedUtc.ToString('yyyyMMddTHHmmssZ'))"
        $failedRoot = Join-Path $repositoryRoot "artifacts\verification\$failedCheckpoint\$testedCommit"
        New-Item -ItemType Directory -Path (Split-Path -Parent $failedRoot) -Force | Out-Null
        Move-Item -LiteralPath $evidenceRoot -Destination $failedRoot
        $evidenceRoot = $failedRoot
    }
}

if ($null -ne $runFailure) {
    throw $runFailure
}

Write-Host "Verification evidence succeeded: $evidenceRoot"
