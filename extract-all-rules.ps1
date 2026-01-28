# Script to extract ALL built-in rule metadata to JSON and generate docs/Rules/README.md.
#
# Source of truth: src/TsqlRefine.Rules/Rules/**/*.cs (RuleMetadata).

$repoRoot = $PSScriptRoot
$rulesPath = Join-Path $repoRoot "src\\TsqlRefine.Rules\\Rules"
$docsRulesRoot = Join-Path $repoRoot "docs\\Rules"

$metadataOutputPath = Join-Path $repoRoot "rules-metadata.json"
$readmeOutputPath = Join-Path $docsRulesRoot "README.md"

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

# Category mapping (folder names under docs/Rules).
$categoryFolders = @{
    "Correctness" = "correctness"
    "Safety" = "safety"
    "Security" = "security"
    "Performance" = "performance"
    "Naming" = "naming"
    "Style" = "style"
    "Transactions" = "transactions"
    "Schema" = "schema"
    "Debug" = "debug"
}

$categoryOrder = @(
    "Correctness",
    "Safety",
    "Security",
    "Performance",
    "Naming",
    "Style",
    "Transactions",
    "Schema",
    "Debug"
)

function ConvertTo-Lf([string]$text)
{
    return ($text -replace "`r`n", "`n" -replace "`r", "`n")
}

function Get-DocFileName([string]$ruleId)
{
    return (($ruleId -replace "/", "-") + ".md")
}

function Get-CategoryDescription([string]$category)
{
    switch ($category)
    {
        "Correctness" { return "Detects code that may produce incorrect results or runtime errors" }
        "Safety" { return "Prevents destructive or dangerous operations" }
        "Security" { return "Identifies security vulnerabilities like SQL injection" }
        "Performance" { return "Flags patterns that can cause performance issues" }
        "Naming" { return "Enforces naming conventions and readability" }
        "Style" { return "Maintains code formatting and consistency" }
        "Transactions" { return "Ensures proper transaction handling and session settings" }
        "Schema" { return "Enforces database schema best practices" }
        "Debug" { return "Controls debug and output statements" }
        default { return "" }
    }
}

function To-YesNo([bool]$value)
{
    if ($value) { return "**Yes**" }
    return "No"
}

function Write-Utf8NoBomLf([string]$path, [string]$content)
{
    $normalized = ConvertTo-Lf($content)
    if (-not $normalized.EndsWith("`n"))
    {
        $normalized += "`n"
    }

    [System.IO.File]::WriteAllText($path, $normalized, $utf8NoBom)
}

Write-Host "Scanning rule sources: $rulesPath"
$ruleFiles = Get-ChildItem -Path $rulesPath -Recurse -File -Filter "*.cs" | Where-Object {
    $_.Name -notmatch "\\.tmp$|\\.new$"
}

$rules = @()

foreach ($ruleFile in $ruleFiles)
{
    $content = Get-Content -Path $ruleFile.FullName -Raw -Encoding UTF8

    $ruleId = if ($content -match 'RuleId:\s*"([^"]+)"') { $matches[1] } else { $null }
    if (-not $ruleId)
    {
        continue
    }

    $description = if ($content -match 'Description:\s*"([^"]+)"') { $matches[1] } else { "" }
    $category = if ($content -match 'Category:\s*"([^"]+)"') { $matches[1] } else { "Style" }
    $severity = if ($content -match 'DefaultSeverity:\s*RuleSeverity\.(\w+)') { $matches[1] } else { "Warning" }
    $fixable = [bool]($content -match 'Fixable:\s*true')

    $categoryFolder = $categoryFolders[$category]
    if (-not $categoryFolder)
    {
        $categoryFolder = "style"
    }

    $rules += [PSCustomObject]@{
        RuleId = $ruleId
        Description = $description
        Category = $category
        CategoryFolder = $categoryFolder
        Severity = $severity
        Fixable = $fixable
        FileName = $ruleFile.Name
    }
}

$rules = $rules | Sort-Object RuleId

# Ensure no duplicate RuleId.
$duplicateIds = $rules | Group-Object RuleId | Where-Object { $_.Count -gt 1 }
if ($duplicateIds.Count -gt 0)
{
    $ids = ($duplicateIds | ForEach-Object { $_.Name }) -join ", "
    throw "Duplicate RuleId detected: $ids"
}

# Write rules-metadata.json (used by other tooling/docs).
$metadataJson = $rules | ConvertTo-Json -Depth 10
Write-Utf8NoBomLf -path $metadataOutputPath -content $metadataJson
Write-Host "Wrote $($rules.Count) rules to $metadataOutputPath"

# Validate docs exist for every rule and no extra docs exist.
$expectedDocRels = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($r in $rules)
{
    $docRel = Join-Path $r.CategoryFolder (Get-DocFileName $r.RuleId)
    $docAbs = Join-Path $docsRulesRoot $docRel
    $null = $expectedDocRels.Add($docRel)

    if (-not (Test-Path $docAbs))
    {
        throw "Missing rule doc: $docRel (RuleId=$($r.RuleId))"
    }
}

$actualDocs = Get-ChildItem -Path $docsRulesRoot -Recurse -File -Filter "*.md" | Where-Object { $_.Name -ne "README.md" }
$extraDocRels = $actualDocs | ForEach-Object { [System.IO.Path]::GetRelativePath($docsRulesRoot, $_.FullName) } | Where-Object { -not $expectedDocRels.Contains($_) }

if ($extraDocRels.Count -gt 0)
{
    $extraList = ($extraDocRels | Sort-Object) -join ", "
    throw "Found docs that do not match any RuleId/Category: $extraList"
}

function Get-RuleLink([object]$rule)
{
    $docPath = "$($rule.CategoryFolder)/$(Get-DocFileName $rule.RuleId)"
    return "[$($rule.RuleId)]($docPath)"
}

function Get-RuleSeverityCounts([object[]]$rs)
{
    $errors = ($rs | Where-Object { $_.Severity -eq "Error" }).Count
    $warnings = ($rs | Where-Object { $_.Severity -eq "Warning" }).Count
    $infos = ($rs | Where-Object { $_.Severity -eq "Information" }).Count

    return [PSCustomObject]@{
        Error = $errors
        Warning = $warnings
        Information = $infos
    }
}

$totalRules = $rules.Count
$fixableRules = ($rules | Where-Object { $_.Fixable }).Count
$severityCounts = Get-RuleSeverityCounts -rs $rules

$categoryCounts = @{}
foreach ($cat in $categoryOrder)
{
    $categoryCounts[$cat] = ($rules | Where-Object { $_.Category -eq $cat }).Count
}

$fixablePercent = if ($totalRules -eq 0) { 0 } else { [math]::Round(($fixableRules * 100.0) / $totalRules) }
$errorPercent = if ($totalRules -eq 0) { 0 } else { [math]::Round(($severityCounts.Error * 100.0) / $totalRules) }
$warningPercent = if ($totalRules -eq 0) { 0 } else { [math]::Round(($severityCounts.Warning * 100.0) / $totalRules) }
$infoPercent = if ($totalRules -eq 0) { 0 } else { [math]::Round(($severityCounts.Information * 100.0) / $totalRules) }

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# TsqlRefine Rules")
$lines.Add("")
$lines.Add('> NOTE: This file is generated by `extract-all-rules.ps1`. Do not edit manually.')
$lines.Add("")
$lines.Add("This document provides a comprehensive overview of all built-in rules in TsqlRefine. TsqlRefine includes $totalRules rules covering various aspects of T-SQL code quality, from correctness and safety to performance and style.")
$lines.Add("")
$lines.Add("## Table of Contents")
$lines.Add("")
$lines.Add("- [Overview](#overview)")
$lines.Add("- [Rule Categories](#rule-categories)")
$lines.Add("- [Rules by Category](#rules-by-category)")
$lines.Add("- [Rules by Severity](#rules-by-severity)")
$lines.Add("- [Fixable Rules](#fixable-rules)")
$lines.Add("- [Individual Rule Documentation](#individual-rule-documentation)")
$lines.Add("")
$lines.Add("## Overview")
$lines.Add("")
$lines.Add("TsqlRefine provides a comprehensive set of rules to enforce T-SQL best practices and detect potential issues in SQL Server code. Each rule has:")
$lines.Add("")
$lines.Add('- **Rule ID**: Unique identifier (e.g., `avoid-select-star`)')
$lines.Add("- **Description**: What the rule checks for")
$lines.Add("- **Category**: Classification of the rule type")
$lines.Add("- **Default Severity**: Error, Warning, or Information")
$lines.Add("- **Fixable**: Whether the rule supports auto-fixing")
$lines.Add("")
$lines.Add("### Rule Statistics")
$lines.Add("")
$lines.Add("- **Total Rules**: $totalRules")
$lines.Add("- **Fixable Rules**: $fixableRules ($fixablePercent%)")
$lines.Add("- **Error Severity**: $($severityCounts.Error) rules ($errorPercent%)")
$lines.Add("- **Warning Severity**: $($severityCounts.Warning) rules ($warningPercent%)")
$lines.Add("- **Information Severity**: $($severityCounts.Information) rules ($infoPercent%)")
$lines.Add("")
$lines.Add("## Rule Categories")
$lines.Add("")
$lines.Add("TsqlRefine organizes rules into the following categories:")
$lines.Add("")
$lines.Add("| Category | Rules | Description |")
$lines.Add("|----------|-------|-------------|")
foreach ($cat in $categoryOrder)
{
    $desc = Get-CategoryDescription $cat
    $count = $categoryCounts[$cat]
    $lines.Add("| **$cat** | $count | $desc |")
}
$lines.Add("")
$lines.Add("## Rules by Category")
$lines.Add("")
foreach ($cat in $categoryOrder)
{
    $catRules = $rules | Where-Object { $_.Category -eq $cat }
    $count = $catRules.Count
    $lines.Add("### $cat ($count rules)")
    $lines.Add("")

    if ($count -eq 0)
    {
        $lines.Add("No rules in this category.")
        $lines.Add("")
        continue
    }

    $lines.Add("| Rule ID | Description | Severity | Fixable |")
    $lines.Add("|---------|-------------|----------|---------|")
    foreach ($r in $catRules)
    {
        $idLink = Get-RuleLink $r
        $lines.Add("| $idLink | $($r.Description) | $($r.Severity) | $(To-YesNo $r.Fixable) |")
    }
    $lines.Add("")
}

$lines.Add("## Rules by Severity")
$lines.Add("")
foreach ($sev in @("Error", "Warning", "Information"))
{
    $sevRules = $rules | Where-Object { $_.Severity -eq $sev }
    $lines.Add("### $sev ($($sevRules.Count) rules)")
    $lines.Add("")

    if ($sevRules.Count -eq 0)
    {
        $lines.Add("No rules with this severity.")
        $lines.Add("")
        continue
    }

    foreach ($r in $sevRules)
    {
        $lines.Add("- $(Get-RuleLink $r)")
    }
    $lines.Add("")
}

$fixable = $rules | Where-Object { $_.Fixable }
$lines.Add("## Fixable Rules")
$lines.Add("")
$lines.Add("The following $($fixable.Count) rules support automatic fixing:")
$lines.Add("")
if ($fixable.Count -gt 0)
{
    $i = 0
    foreach ($r in $fixable)
    {
        $i++
        $lines.Add("$i. $(Get-RuleLink $r) - $($r.Description)")
    }
    $lines.Add("")
}

$lines.Add('To apply auto-fixes, use the `fix` command:')
$lines.Add("")
$lines.Add('```powershell')
$lines.Add("dotnet run --project src/TsqlRefine.Cli -c Release -- fix --write file.sql")
$lines.Add('```')
$lines.Add("")
$lines.Add("## Individual Rule Documentation")
$lines.Add("")
$lines.Add("For detailed information about each rule, including examples and configuration options, see the individual rule documentation files in the category subdirectories:")
$lines.Add("")
$lines.Add("- [Correctness Rules](correctness/)")
$lines.Add("- [Safety Rules](safety/)")
$lines.Add("- [Security Rules](security/)")
$lines.Add("- [Performance Rules](performance/)")
$lines.Add("- [Naming Rules](naming/)")
$lines.Add("- [Style Rules](style/)")
$lines.Add("- [Transactions Rules](transactions/)")
$lines.Add("- [Schema Rules](schema/)")
$lines.Add("- [Debug Rules](debug/)")
$lines.Add("")
$lines.Add("## Configuration")
$lines.Add("")
$lines.Add('Rules can be configured in `tsqlrefine.json` or via ruleset files. See [Configuration Documentation](../configuration.md) for details.')
$lines.Add("")
$lines.Add("### Disabling Rules")
$lines.Add("")
$lines.Add("To disable specific rules, use a ruleset file:")
$lines.Add("")
$lines.Add('```json')
$lines.Add("{")
$lines.Add('  "rules": [')
$lines.Add('    { "id": "avoid-select-star", "enabled": false }')
$lines.Add("  ]")
$lines.Add("}")
$lines.Add('```')
$lines.Add("")
$lines.Add("### Preset Rulesets")
$lines.Add("")
$lines.Add("TsqlRefine provides three preset rulesets:")
$lines.Add("")
$lines.Add("- **recommended**: Balanced set of rules for general use (default)")
$lines.Add("- **strict**: All rules enabled for maximum code quality")
$lines.Add("- **security-only**: Only security and safety-critical rules")
$lines.Add("")
$lines.Add('```powershell')
$lines.Add("dotnet run --project src/TsqlRefine.Cli -- lint --preset strict file.sql")
$lines.Add('```')
$lines.Add("")
$lines.Add("## Contributing")
$lines.Add("")
$lines.Add("To add a new rule, see [CLAUDE.md](../../CLAUDE.md#adding-a-new-built-in-rule) for implementation guidelines.")

Write-Utf8NoBomLf -path $readmeOutputPath -content ($lines -join "`n")
Write-Host "Wrote $readmeOutputPath"
