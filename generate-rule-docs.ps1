# Script to extract rule metadata and generate documentation

$rulesPath = "src\TsqlRefine.Rules\Rules"
$testsPath = "tests\TsqlRefine.Rules.Tests"
$outputBase = "docs\Rules"

# Category mapping based on RuleMetadata.Category
$categoryFolders = @{
    "Correctness" = "correctness"
    "Safety" = "safety"
    "Security" = "security"
    "Performance" = "performance"
    "Naming" = "naming"
    "Style" = "style"
    "Transactions" = "transactions"
    "Schema Design" = "schema"
    "Debug" = "debug"
    "Maintainability" = "style"  # Map Maintainability to style
}

# Get all rule files
$ruleFiles = Get-ChildItem -Path $rulesPath -Filter "*.cs" | Where-Object {
    $_.Name -notmatch '\.tmp|\.new'
}

Write-Host "Found $($ruleFiles.Count) rule files"

foreach ($ruleFile in $ruleFiles) {
    $content = Get-Content $ruleFile.FullName -Raw

    # Extract metadata using regex
    if ($content -match 'RuleId:\s*"([^"]+)"') {
        $ruleId = $matches[1]
    } else {
        Write-Warning "No RuleId found in $($ruleFile.Name)"
        continue
    }

    if ($content -match 'Description:\s*"([^"]+)"') {
        $description = $matches[1]
    } else {
        $description = ""
    }

    if ($content -match 'Category:\s*"([^"]+)"') {
        $category = $matches[1]
    } else {
        $category = "Style"
    }

    if ($content -match 'DefaultSeverity:\s*RuleSeverity\.(\w+)') {
        $severity = $matches[1]
    } else {
        $severity = "Warning"
    }

    if ($content -match 'Fixable:\s*(true|false)') {
        $fixable = if ($matches[1] -eq 'true') { 'Yes' } else { 'No' }
    } else {
        $fixable = "No"
    }

    # Map category to folder
    $categoryFolder = $categoryFolders[$category]
    if (-not $categoryFolder) {
        $categoryFolder = "style"
        Write-Warning "Unknown category '$category' for rule $ruleId, using 'style'"
    }

    Write-Host "Processing: $ruleId ($category -> $categoryFolder)"

    # Output: Just print the data, don't generate files yet
    Write-Host "  ID: $ruleId"
    Write-Host "  Description: $description"
    Write-Host "  Category: $category -> $categoryFolder"
    Write-Host "  Severity: $severity"
    Write-Host "  Fixable: $fixable"
    Write-Host ""
}
