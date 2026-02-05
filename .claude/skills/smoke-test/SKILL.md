---
name: smoke-test
description: End-to-end verification of tsqlrefine CLI commands. Use when: validating CLI works correctly, testing before release, verifying exit codes, or checking all commands (lint, format, fix, list-rules, init, print-config, list-plugins).
---

# CLI Smoke Test

Verify all CLI commands work correctly.

## Test Cases

### lint

```powershell
# Clean SQL (exit 0)
echo "SELECT id FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --preset pragmatic

# SQL with violations (exit 1)
echo "SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin

# Invalid SQL (exit 2)
echo "SELECT FROM WHERE" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin

# JSON output
echo "SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --output json
```

### format

```powershell
# Keyword casing
echo "select * from users" | dotnet run --project src/TsqlRefine.Cli -c Release -- format --stdin
# Expected: SELECT * FROM users
```

### fix

```powershell
# Apply fixes
echo "select * from users" | dotnet run --project src/TsqlRefine.Cli -c Release -- fix --stdin --rule normalize-keyword-casing
```

### list-rules

```powershell
# Text output
dotnet run --project src/TsqlRefine.Cli -c Release -- list-rules

# JSON output
dotnet run --project src/TsqlRefine.Cli -c Release -- list-rules --output json
```

### Other commands

```powershell
# print-config
dotnet run --project src/TsqlRefine.Cli -c Release -- print-config

# list-plugins
dotnet run --project src/TsqlRefine.Cli -c Release -- list-plugins

# init (creates tsqlrefine.json)
dotnet run --project src/TsqlRefine.Cli -c Release -- init --path $env:TEMP/test-config
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Violations found |
| 2 | Parse error |
| 3 | Config error |
| 4 | Runtime exception |

## Output

Report pass/fail for each test case with exit code verification.
