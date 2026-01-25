# Claude Code post-response hook (PowerShell version for Windows)
# Runs format, build, and test after Claude makes changes

$ErrorActionPreference = "Stop"

Write-Host "🔄 Running post-development checks..." -ForegroundColor Cyan
Write-Host ""

try {
    # Step 1: Build
    Write-Host "📦 Building solution..." -ForegroundColor Yellow
    & dotnet build src/TsqlRefine.sln -c Release --nologo --verbosity minimal

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ Build succeeded" -ForegroundColor Green
    Write-Host ""

    # Step 2: Test
    Write-Host "🧪 Running tests..." -ForegroundColor Yellow
    & dotnet test src/TsqlRefine.sln -c Release --nologo --verbosity minimal --no-build

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Tests failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ Tests passed" -ForegroundColor Green
    Write-Host ""

    Write-Host "✅ All checks passed!" -ForegroundColor Green
    exit 0
}
catch {
    Write-Host "❌ Hook failed: $_" -ForegroundColor Red
    exit 1
}
