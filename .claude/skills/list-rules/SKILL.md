---
name: list-rules
description: List available tsqlrefine rules with filtering. Use when: discovering available rules, checking rule categories, finding fixable rules, or exploring rule metadata.
---

# List Rules

List rules with `tsqlrefine list-rules`.

## Commands

```powershell
# All rules (text)
dotnet run --project src/TsqlRefine.Cli -c Release -- list-rules

# JSON output
dotnet run --project src/TsqlRefine.Cli -c Release -- list-rules --output json
```

## Rule Categories

| Category | Description |
|----------|-------------|
| **Safety** | Prevent data loss/corruption |
| **Correctness** | Fix logical errors |
| **Performance** | Optimize query performance |
| **Style** | Code consistency |
| **Security** | Security vulnerabilities |
| **Schema** | Schema design issues |
| **Transactions** | Transaction handling |
| **Debug** | Debug/development issues |

## Presets

| Preset | Rules | Use Case |
|--------|-------|----------|
| `recommended` | 49 | Balanced production |
| `strict` | 86 | Maximum enforcement |
| `pragmatic` | 30 | Minimal noise |
| `security-only` | 10 | Security focus |

## Output

Show rule ID, description, severity, and fixable status for each rule.
