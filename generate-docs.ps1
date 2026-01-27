# Generate documentation for all rules

param(
    [switch]$Force = $false
)

$ErrorActionPreference = "Stop"

# Load rule metadata
$rulesMetadata = Get-Content "rules-metadata.json" -Raw | ConvertFrom-Json

# Existing documented rules
$existingRules = @(
    "avoid-ambiguous-datetime-literal",
    "avoid-atat-identity",
    "avoid-exec-dynamic-sql",
    "avoid-heap-table",
    "avoid-implicit-conversion-in-predicate",
    "avoid-magic-convert-style-for-datetime",
    "avoid-merge",
    "avoid-nolock",
    "avoid-null-comparison",
    "semantic-alias-scope-violation"
)

# Category mapping for documentation folders
$categoryMap = @{
    "correctness" = "Correctness"
    "safety" = "Safety"
    "security" = "Security"
    "performance" = "Performance"
    "naming" = "Naming"
    "style" = "Style"
    "transactions" = "Transactions"
    "schema" = "Schema Design"
    "debug" = "Debug"
}

function Get-TestExamples {
    param(
        [string]$RuleId,
        [string]$FileName
    )

    $testFileName = $FileName -replace 'Rule\.cs$', 'RuleTests.cs'
    $testPath = "tests\TsqlRefine.Rules.Tests\$testFileName"

    $badExample = $null
    $goodExample = $null

    if (Test-Path $testPath) {
        $testContent = Get-Content $testPath -Raw

        # Extract bad example (test that returns diagnostic)
        if ($testContent -match 'var sql = "([^"]+)";\s*var context.*\n.*\n.*var diagnostics = rule\.Analyze\(context\)\.ToArray\(\);\s*\n\s*Assert\.Single\(diagnostics\)') {
            $badExample = $matches[1]
        }
        elseif ($testContent -match 'var sql = @"([^"]+)";\s*var context.*\n.*\n.*var diagnostics = rule\.Analyze\(context\)\.ToArray\(\);\s*\n\s*Assert\.Single\(diagnostics\)') {
            $badExample = $matches[1]
        }
        elseif ($testContent -match 'var sql = "([^"]+)".*Assert\.(Single|NotEmpty)\(diagnostics\)') {
            $badExample = $matches[1]
        }

        # Extract good example (test that returns no diagnostic)
        if ($testContent -match 'var sql = "([^"]+)";\s*var context.*\n.*\n.*var diagnostics = rule\.Analyze\(context\)\.ToArray\(\);\s*\n\s*Assert\.Empty\(diagnostics\)') {
            $goodExample = $matches[1]
        }
        elseif ($testContent -match 'var sql = @"([^"]+)";\s*var context.*\n.*\n.*var diagnostics = rule\.Analyze\(context\)\.ToArray\(\);\s*\n\s*Assert\.Empty\(diagnostics\)') {
            $goodExample = $matches[1]
        }
    }

    return @{
        Bad = $badExample
        Good = $goodExample
    }
}

function Get-Rationale {
    param(
        [string]$Description,
        [string]$Category
    )

    # Generate rationale based on category and description
    $rationales = @{
        "Correctness" = "This rule prevents code that may produce incorrect results or runtime errors. Following this rule helps ensure your SQL code behaves as expected and produces reliable results."
        "Safety" = "This rule prevents destructive or dangerous operations that could lead to data loss or corruption. Following this rule helps protect your database from accidental or unintended modifications."
        "Security" = "This rule identifies security vulnerabilities that could be exploited by attackers. Following this rule helps protect your database from SQL injection and other security threats."
        "Performance" = "This rule identifies patterns that can cause performance issues. Following this rule helps ensure your queries run efficiently and scale well with data growth."
        "Naming" = "This rule enforces naming conventions that improve code readability and maintainability. Following this rule makes your code easier to understand for other developers."
        "Style" = "This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain."
        "Transactions" = "This rule ensures proper transaction handling and session settings. Following this rule helps prevent data inconsistency and ensures reliable transaction management."
        "Schema Design" = "This rule enforces database schema best practices. Following this rule helps create robust, maintainable database schemas."
        "Debug" = "This rule controls debug and output statements. Following this rule helps maintain clean, production-ready code."
    }

    $categoryName = $categoryMap[$Category]
    if (-not $categoryName) {
        $categoryName = "Style"
    }

    return $rationales[$categoryName]
}

function Generate-RuleDoc {
    param(
        [PSCustomObject]$Rule
    )

    $ruleId = $Rule.RuleId
    $categoryFolder = $Rule.CategoryFolder
    # Replace / with - for file names (e.g., semantic/alias -> semantic-alias)
    $safeRuleId = $ruleId -replace '/', '-'
    $outputDir = "docs\Rules\$categoryFolder"
    $outputFile = "$outputDir\$safeRuleId.md"

    # Skip if already exists and not forcing
    if ((Test-Path $outputFile) -and -not $Force) {
        Write-Host "Skipping $ruleId (already exists)" -ForegroundColor Yellow
        return
    }

    # Create directory if it doesn't exist
    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }

    # Get examples from tests
    $examples = Get-TestExamples -RuleId $ruleId -FileName $Rule.FileName

    # Generate rationale
    $rationale = Get-Rationale -Description $Rule.Description -Category $Rule.CategoryFolder

    # Get rule title (convert kebab-case to Title Case)
    $title = ($ruleId -split '[/-]' | ForEach-Object {
        $_.Substring(0,1).ToUpper() + $_.Substring(1).ToLower()
    }) -join ' '

    # Generate documentation
    $doc = @"
# $title

**Rule ID:** ``$ruleId``
**Category:** $($Rule.Category)
**Severity:** $($Rule.Severity)
**Fixable:** $(if ($Rule.Fixable) { 'Yes' } else { 'No' })

## Description

$($Rule.Description)

## Rationale

$rationale

## Examples

### Bad

``````sql
$(if ($examples.Bad) { $examples.Bad } else { "-- Example showing rule violation" })
``````

### Good

``````sql
$(if ($examples.Good) { $examples.Good } else { "-- Example showing compliant code" })
``````

## Configuration

To disable this rule, add it to your ``tsqlrefine.json``:

``````json
{
  "ruleset": "custom-ruleset.json"
}
``````

In ``custom-ruleset.json``:

``````json
{
  "rules": [
    { "id": "$ruleId", "enabled": false }
  ]
}
``````

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
"@

    # Write file
    $doc | Out-File -FilePath $outputFile -Encoding UTF8
    Write-Host "Generated: $outputFile" -ForegroundColor Green
}

# Generate documentation for all rules
$count = 0
foreach ($rule in $rulesMetadata) {
    if ($existingRules -notcontains $rule.RuleId -or $Force) {
        Generate-RuleDoc -Rule $rule
        $count++
    }
}

Write-Host "`nGenerated $count documentation files" -ForegroundColor Cyan
