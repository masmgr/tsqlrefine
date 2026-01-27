# Enhance documentation with real examples from test files

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

    # Extract InlineData examples from test methods that return diagnostics (bad examples)
    # Pattern: [InlineData("...")] followed by method that checks Assert.Single or Assert.NotEmpty
    $badPattern = '\[InlineData\("([^"]+)"\)\](?:[^\n]*\n)*[^\n]*(?:ReturnsDiagnostic|WhenViolating|Returns.*Diagnostic)'
    if ($content -match $badPattern) {
        $matches = [regex]::Matches($content, '\[InlineData\("([^"]+)"\)\](?:[^\n]*\n){0,10}[^\n]*(?:ReturnsDiagnostic|WhenViolating|Returns.*Diagnostic)')
        foreach ($match in $matches) {
            if ($match.Groups[1].Value) {
                $badExamples += $match.Groups[1].Value
            }
        }
    }

    # Extract InlineData examples from test methods that return empty (good examples)
    $goodPattern = '\[InlineData\("([^"]+)"\)\](?:[^\n]*\n)*[^\n]*(?:ReturnsEmpty|WhenNotViolating|WhenNo)'
    if ($content -match $goodPattern) {
        $matches = [regex]::Matches($content, '\[InlineData\("([^"]+)"\)\](?:[^\n]*\n){0,10}[^\n]*(?:ReturnsEmpty|WhenNotViolating|WhenNo)')
        foreach ($match in $matches) {
            if ($match.Groups[1].Value) {
                $goodExamples += $match.Groups[1].Value
            }
        }
    }

    # If no InlineData found, try to extract from simple var sql = "..." patterns
    if ($badExamples.Count -eq 0) {
        if ($content -match 'var sql = "([^"]+)";\s*var context[^\n]*\n[^\n]*\n[^\n]*Assert\.Single') {
            $badExamples += $matches[1]
        }
    }

    if ($goodExamples.Count -eq 0) {
        if ($content -match 'var sql = "([^"]+)";\s*var context[^\n]*\n[^\n]*\n[^\n]*Assert\.Empty') {
            $goodExamples += $matches[1]
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

    # Update bad example
    if ($BadExample) {
        $content = $content -replace '```sql\r?\n-- Example showing rule violation\r?\n```', "``````sql`n$BadExample`n``````"
    }

    # Update good example
    if ($GoodExample) {
        $content = $content -replace '```sql\r?\n-- Example showing compliant code\r?\n```', "``````sql`n$GoodExample`n``````"
    }

    $content | Out-File -FilePath $DocPath -Encoding UTF8 -NoNewline
}

# Load rule metadata
$rulesMetadata = Get-Content "rules-metadata.json" -Raw | ConvertFrom-Json

$updated = 0
$notFound = 0

foreach ($rule in $rulesMetadata) {
    $ruleId = $rule.RuleId
    $safeRuleId = $ruleId -replace '/', '-'
    $categoryFolder = $rule.CategoryFolder
    $docPath = "docs\Rules\$categoryFolder\$safeRuleId.md"

    if (-not (Test-Path $docPath)) {
        Write-Host "Doc not found: $docPath" -ForegroundColor Yellow
        continue
    }

    # Find corresponding test file
    $testFileName = $rule.FileName -replace 'Rule\.cs$', 'RuleTests.cs'
    $testPath = "tests\TsqlRefine.Rules.Tests\$testFileName"

    if (-not (Test-Path $testPath)) {
        Write-Host "Test file not found for $ruleId" -ForegroundColor Yellow
        $notFound++
        continue
    }

    # Extract examples
    $examples = Extract-TestExamples -TestFilePath $testPath

    if ($examples.Bad -or $examples.Good) {
        Update-DocFile -DocPath $docPath -BadExample $examples.Bad -GoodExample $examples.Good
        Write-Host "Updated: $ruleId" -ForegroundColor Green
        $updated++
    } else {
        Write-Host "No examples found for: $ruleId" -ForegroundColor Yellow
        $notFound++
    }
}

Write-Host "`nUpdated $updated documentation files" -ForegroundColor Cyan
Write-Host "Not found/no examples: $notFound" -ForegroundColor Yellow
