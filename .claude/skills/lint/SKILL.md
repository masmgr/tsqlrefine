---
name: lint
description: Lint SQL code using tsqlrefine CLI. Use when: checking SQL for violations, running static analysis, validating T-SQL code quality. Supports presets (recommended, strict, pragmatic, security-only), severity filtering, and specific rule checks.
---

# Quick SQL Lint

Lint SQL with `tsqlrefine lint`.

## Commands

```powershell
# Inline SQL
echo "SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin

# File
dotnet run --project src/TsqlRefine.Cli -c Release -- lint path/to/file.sql

# With preset
echo "SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --preset strict

# Filter by severity
echo "SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --severity error

# Specific rule only
echo "SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --rule avoid-select-star

# JSON output
echo "SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --output json
```

## Presets

| Preset | Description |
|--------|-------------|
| `recommended` | Balanced for production (default) |
| `strict` | Maximum enforcement |
| `pragmatic` | Minimal noise |
| `security-only` | Security focus only |

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | No violations |
| 1 | Violations found |
| 2 | Parse error |

## Output

Report violations with `file:line:col: severity: message (rule-id)` format.
