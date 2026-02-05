---
name: smoke-test
description: Run end-to-end smoke tests on the CLI. Use when asked to verify the CLI works, test before a release, or validate all commands.
---

# CLI Smoke Test

Build and verify all CLI commands produce expected exit codes and output.

## Workflow

1. Build: `dotnet build src/TsqlRefine.sln -c Release`
2. Run each test case below
3. Verify exit codes match expectations
4. Report pass/fail summary for each command

## Test Cases

Run each command and check its exit code (`$LASTEXITCODE` in PowerShell).

### lint
```powershell
# Exit 0: clean SQL
echo "SELECT id FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --preset pragmatic

# Exit 1: SQL with violations
echo "SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin

# Exit 2: invalid SQL
echo "SELECT FROM WHERE" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin

# JSON output
echo "SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --output json
```

### format
```powershell
# Keyword casing (expect uppercase output)
echo "select * from users" | dotnet run --project src/TsqlRefine.Cli -c Release -- format --stdin
```

### fix
```powershell
echo "select * from users" | dotnet run --project src/TsqlRefine.Cli -c Release -- fix --stdin --rule normalize-keyword-casing
```

### list-rules
```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- list-rules
dotnet run --project src/TsqlRefine.Cli -c Release -- list-rules --output json
```

### Other commands
```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- print-config
dotnet run --project src/TsqlRefine.Cli -c Release -- list-plugins
dotnet run --project src/TsqlRefine.Cli -c Release -- init --path $env:TEMP/test-config
```

## Expected Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Violations found |
| 2 | Parse error |
| 3 | Config error |
| 4 | Runtime exception |
