# Enhanced script to extract examples from test files with better pattern matching

$ErrorActionPreference = "Stop"

function Extract-TestExamples {
    param(
        [string]$TestFilePath
    )

    if (-not (Test-Path $TestFilePath)) {
        return $null
    }

    $content = Get-Content $TestFilePath -Raw

    $badExamples = @()
    $goodExamples = @()

    # Pattern 1: [InlineData("...")] from Theory tests that return diagnostics
    $matches = [regex]::Matches($content, '\[InlineData\("([^"]+)"\)\](?:[^\n]*\n){0,15}[^\n]*(?:ReturnsDiagnostic|WhenViolating|Returns.*Diagnostic)')
    foreach ($match in $matches) {
        if ($match.Groups[1].Value) {
            $badExamples += $match.Groups[1].Value
        }
    }

    # Pattern 2: [InlineData("...")] from Theory tests that return empty
    $matches = [regex]::Matches($content, '\[InlineData\("([^"]+)"\)\](?:[^\n]*\n){0,15}[^\n]*(?:ReturnsEmpty|WhenNotViolating|WhenNo|ReturnsNoDiagnostic)')
    foreach ($match in $matches) {
        if ($match.Groups[1].Value) {
            $goodExamples += $match.Groups[1].Value
        }
    }

    # Pattern 3: var sql = "..." or const string sql = "..." that returns diagnostic
    if ($badExamples.Count -eq 0) {
        $matches = [regex]::Matches($content, '(?:var|const string) sql = "([^"]+)";\s*(?:[^\n]*\n){0,10}[^\n]*Assert\.(?:Single|NotEmpty)\(diagnostics')
        foreach ($match in $matches) {
            if ($match.Groups[1].Value) {
                $badExamples += $match.Groups[1].Value
            }
        }
    }

    # Pattern 4: var sql = "..." or const string sql = "..." that returns empty
    if ($goodExamples.Count -eq 0) {
        $matches = [regex]::Matches($content, '(?:var|const string) sql = "([^"]+)";\s*(?:[^\n]*\n){0,10}[^\n]*Assert\.Empty\(diagnostics')
        foreach ($match in $matches) {
            if ($match.Groups[1].Value) {
                $goodExamples += $match.Groups[1].Value
            }
        }
    }

    return @{
        Bad = if ($badExamples.Count -gt 0) { $badExamples[0] } else { $null }
        Good = if ($goodExamples.Count -gt 0) { $goodExamples[0] } else { $null }
    }
}

function Update-DocFile {
    param(
        [string]$DocPath,
        [string]$BadExample,
        [string]$GoodExample
    )

    $content = Get-Content $DocPath -Raw

    # Update bad example - handle both patterns
    if ($BadExample) {
        $content = $content -replace '```sql\r?\n-- Example showing rule violation\r?\n```', "``````sql`n$BadExample`n``````"
        # Also try to replace if it already has some content
        $content = $content -replace '(### Bad\r?\n\r?\n```sql\r?\n)[^\n]*(\r?\n```)', "`${1}$BadExample`${2}"
    }

    # Update good example
    if ($GoodExample) {
        $content = $content -replace '```sql\r?\n-- Example showing compliant code\r?\n```', "``````sql`n$GoodExample`n``````"
        # Also try to replace if it already has some content
        $content = $content -replace '(### Good\r?\n\r?\n```sql\r?\n)[^\n]*(\r?\n```)', "`${1}$GoodExample`${2}"
    }

    $content | Out-File -FilePath $DocPath -Encoding UTF8 -NoNewline
}

# Load rule metadata
$rulesMetadata = Get-Content "rules-metadata.json" -Raw | ConvertFrom-Json

$updated = 0
$notFound = 0
$noExamples = @()

foreach ($rule in $rulesMetadata) {
    $ruleId = $rule.RuleId
    $safeRuleId = $ruleId -replace '/', '-'
    $categoryFolder = $rule.CategoryFolder
    $docPath = "docs\Rules\$categoryFolder\$safeRuleId.md"

    if (-not (Test-Path $docPath)) {
        continue
    }

    # Find corresponding test file
    $testFileName = $rule.FileName -replace 'Rule\.cs$', 'RuleTests.cs'
    $testPath = "tests\TsqlRefine.Rules.Tests\$testFileName"

    if (-not (Test-Path $testPath)) {
        $notFound++
        continue
    }

    # Extract examples
    $examples = Extract-TestExamples -TestFilePath $testPath

    if ($examples.Bad -or $examples.Good) {
        Update-DocFile -DocPath $docPath -BadExample $examples.Bad -GoodExample $examples.Good
        Write-Host "Updated: $ruleId (Bad: $(if($examples.Bad){'Yes'}else{'No'}), Good: $(if($examples.Good){'Yes'}else{'No'}))" -ForegroundColor Green
        $updated++
    } else {
        $noExamples += $ruleId
        Write-Host "No examples found for: $ruleId" -ForegroundColor Yellow
    }
}

Write-Host "`nUpdated $updated documentation files" -ForegroundColor Cyan
Write-Host "Not found: $notFound" -ForegroundColor Yellow
Write-Host "No examples: $($noExamples.Count)" -ForegroundColor Yellow

if ($noExamples.Count -gt 0) {
    Write-Host "`nRules without examples:" -ForegroundColor Yellow
    $noExamples | ForEach-Object { Write-Host "  - $_" }
}
