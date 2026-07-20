#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CoveragePath,

    [string]$BaselinePath = (Join-Path $PSScriptRoot 'coverage-baseline.json')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $CoveragePath)) {
    throw "Coverage report not found: $CoveragePath"
}

[xml]$coverage = Get-Content -Raw -LiteralPath $CoveragePath
$baseline = Get-Content -Raw -LiteralPath $BaselinePath | ConvertFrom-Json
$lineRate = [double]$coverage.coverage.'line-rate'
$branchRate = [double]$coverage.coverage.'branch-rate'
$tolerance = [double]$baseline.tolerance

Write-Host ("Coverage: lines {0:P2}, branches {1:P2}" -f $lineRate, $branchRate)
Write-Host ("Baseline: lines {0:P2}, branches {1:P2}, tolerance {2:P2}" -f `
    [double]$baseline.lineRate, [double]$baseline.branchRate, $tolerance)

if ($lineRate + $tolerance -lt [double]$baseline.lineRate) {
    throw "Line coverage regressed below the committed baseline."
}

if ($branchRate + $tolerance -lt [double]$baseline.branchRate) {
    throw "Branch coverage regressed below the committed baseline."
}
