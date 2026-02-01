---
name: fix
description: Apply auto-fixes to SQL code using tsqlrefine. Use when: fixing SQL violations automatically, applying keyword normalization, correcting SQL patterns. Complements /lint and /format skills. Supports specific rule fixes and presets.
---

# Quick SQL Fix

Apply auto-fixes with `tsqlrefine fix`.

## Commands

```powershell
# Fix inline SQL
echo "select * from users where id=1" | dotnet run --project src/TsqlRefine.Cli -c Release -- fix --stdin

# Fix file (writes in place)
dotnet run --project src/TsqlRefine.Cli -c Release -- fix path/to/file.sql

# Fix specific rule only
echo "select * from users" | dotnet run --project src/TsqlRefine.Cli -c Release -- fix --stdin --rule normalize-keyword-casing

# Fix with preset
echo "select * from users" | dotnet run --project src/TsqlRefine.Cli -c Release -- fix --stdin --preset strict

# JSON output (shows diagnostics)
echo "select * from users" | dotnet run --project src/TsqlRefine.Cli -c Release -- fix --stdin --output json
```

## Common Fixable Rules

| Rule ID | Fix |
|---------|-----|
| `normalize-keyword-casing` | Uppercase SQL keywords |
| `trailing-semicolon` | Add missing semicolons |
| `escape-keyword-identifier` | Bracket reserved word identifiers |

## Output

- Fixed SQL to stdout (for stdin input) or written to file
- List of applied fixes
- Remaining violations (not auto-fixable)

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success (no violations remaining) |
| 1 | Fixes applied but violations remain |
| 2 | Parse error |
