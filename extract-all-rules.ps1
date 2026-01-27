# Script to extract ALL rule metadata to JSON

$rulesPath = "src\TsqlRefine.Rules\Rules"

# Category mapping
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
    "Maintainability" = "style"
}

$allRules = @()

# Get all rule files
$ruleFiles = Get-ChildItem -Path $rulesPath -Filter "*.cs" | Where-Object {
    $_.Name -notmatch '\.tmp|\.new'
}

foreach ($ruleFile in $ruleFiles) {
    $content = Get-Content $ruleFile.FullName -Raw

    # Extract metadata
    $ruleId = if ($content -match 'RuleId:\s*"([^"]+)"') { $matches[1] } else { $null }
    $description = if ($content -match 'Description:\s*"([^"]+)"') { $matches[1] } else { "" }
    $category = if ($content -match 'Category:\s*"([^"]+)"') { $matches[1] } else { "Style" }
    $severity = if ($content -match 'DefaultSeverity:\s*RuleSeverity\.(\w+)') { $matches[1] } else { "Warning" }
    $fixable = if ($content -match 'Fixable:\s*true') { $true } else { $false }

    if ($ruleId) {
        $categoryFolder = $categoryFolders[$category]
        if (-not $categoryFolder) {
            $categoryFolder = "style"
        }

        $allRules += [PSCustomObject]@{
            RuleId = $ruleId
            Description = $description
            Category = $category
            CategoryFolder = $categoryFolder
            Severity = $severity
            Fixable = $fixable
            FileName = $ruleFile.Name
        }
    }
}

# Sort by RuleId
$allRules = $allRules | Sort-Object RuleId

# Output as JSON
$allRules | ConvertTo-Json -Depth 10 | Out-File "rules-metadata.json" -Encoding UTF8
Write-Host "Extracted $($allRules.Count) rules to rules-metadata.json"
