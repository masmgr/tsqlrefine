#Requires -Version 5.1
<#
.SYNOPSIS
    Runs tests with code coverage and generates an HTML report.

.DESCRIPTION
    This script runs all tests with code coverage collection using coverlet,
    then generates an HTML report using ReportGenerator.

.EXAMPLE
    .\scripts\run-coverage.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$resultsDir = Join-Path $repoRoot 'TestResults'

# Clean previous results
if (Test-Path $resultsDir) {
    Remove-Item -Path $resultsDir -Recurse -Force
}

Write-Host 'Running tests with coverage...' -ForegroundColor Cyan
dotnet test (Join-Path $repoRoot 'src/TsqlRefine.sln') `
    -c Release `
    --collect:"XPlat Code Coverage" `
    --results-directory $resultsDir

if ($LASTEXITCODE -ne 0) {
    Write-Error 'Tests failed'
    exit $LASTEXITCODE
}

# Install ReportGenerator if not already installed
$reportGenInstalled = dotnet tool list -g | Select-String 'dotnet-reportgenerator-globaltool'
if (-not $reportGenInstalled) {
    Write-Host 'Installing ReportGenerator...' -ForegroundColor Cyan
    dotnet tool install -g dotnet-reportgenerator-globaltool
}

Write-Host 'Generating coverage report...' -ForegroundColor Cyan
$coverageFiles = Get-ChildItem -Path $resultsDir -Filter 'coverage.cobertura.xml' -Recurse
if ($coverageFiles.Count -eq 0) {
    Write-Error 'No coverage files found'
    exit 1
}

$reportDir = Join-Path $resultsDir 'CoverageReport'
reportgenerator `
    -reports:"$resultsDir/**/coverage.cobertura.xml" `
    -targetdir:$reportDir `
    -reporttypes:'Html'

$indexPath = Join-Path $reportDir 'index.html'
if (Test-Path $indexPath) {
    Write-Host "Opening coverage report: $indexPath" -ForegroundColor Green
    Start-Process $indexPath
} else {
    Write-Warning 'Coverage report not generated'
}
