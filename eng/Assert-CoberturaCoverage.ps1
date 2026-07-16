param(
    [Parameter(Mandatory = $true)]
    [string]$CoveragePath,

    [string]$PackageName = "Convergence.Framework",

    [double]$MinimumLineRate = 0.90,

    [double]$MinimumBranchRate = 0.70
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $CoveragePath -PathType Leaf)) {
    throw "Cobertura report '$CoveragePath' does not exist."
}

[xml]$coverage = Get-Content -LiteralPath $CoveragePath
$packages = @(
    @($coverage.coverage.packages.package) |
        Where-Object { $_.name -eq $PackageName }
)
if ($packages.Count -ne 1) {
    $available = (@($coverage.coverage.packages.package) | ForEach-Object { $_.name }) -join ", "
    throw "Expected one coverage package '$PackageName' but found $($packages.Count). Available packages: $available"
}

$package = $packages[0]
$culture = [System.Globalization.CultureInfo]::InvariantCulture
$lineRate = [double]::Parse($package.'line-rate', $culture)
$branchRate = [double]::Parse($package.'branch-rate', $culture)

Write-Host ("Framework coverage: lines {0:P2}; branches {1:P2}." -f $lineRate, $branchRate)
if ($lineRate -lt $MinimumLineRate) {
    throw ("Framework line coverage {0:P2} is below the required {1:P2}." -f $lineRate, $MinimumLineRate)
}

if ($branchRate -lt $MinimumBranchRate) {
    throw ("Framework branch coverage {0:P2} is below the required {1:P2}." -f $branchRate, $MinimumBranchRate)
}
