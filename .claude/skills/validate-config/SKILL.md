---
name: validate-config
description: Validate tsqlrefine configuration files against JSON schemas. Use when: checking tsqlrefine.json is valid, validating ruleset files, finding config errors, or verifying schema compliance.
---

# Configuration Validator

Validate config files against schemas in `schemas/`.

## Schema Files

| Config Type | Schema |
|-------------|--------|
| Main config | `schemas/tsqlrefine.schema.json` |
| Ruleset | `schemas/ruleset.schema.json` |

## Main Config Properties (tsqlrefine.json)

| Property | Type | Values |
|----------|------|--------|
| `compatLevel` | integer | 100, 110, 120, 150, 160 |
| `ruleset` | string | Path to ruleset file |
| `plugins` | array | Plugin configurations |
| `formatting` | object | Formatting options |

## Validation Workflow

1. Parse JSON and check syntax errors
2. Validate required properties exist
3. Check referenced files exist (ruleset, plugins)
4. Run CLI to verify config loads correctly

## Commands

```powershell
# Validate config loads correctly (reports config errors with exit code 3)
dotnet run --project src/TsqlRefine.Cli -c Release -- print-config

# Check JSON syntax
Get-Content tsqlrefine.json | ConvertFrom-Json

# Check ruleset file exists
Test-Path "rulesets/recommended.json"

# List available presets
Get-ChildItem rulesets/*.json
```

## Common Errors

| Error | Fix |
|-------|-----|
| Invalid compatLevel | Use 100, 110, 120, 150, or 160 |
| Ruleset not found | Check path is correct |
| Invalid JSON syntax | Fix JSON formatting |
| Unknown property | Remove or fix property name |
