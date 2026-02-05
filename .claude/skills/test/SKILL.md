---
name: test
description: Run .NET tests for tsqlrefine. Use when: running unit tests, verifying rule implementations, checking for test failures, or validating code changes. Supports filtering by test name, project, or pattern.
---

# Test Runner

Run tests with `dotnet test`.

## Commands

```powershell
# All tests
dotnet test src/TsqlRefine.sln -c Release

# Filter by name
dotnet test src/TsqlRefine.sln -c Release --filter "FullyQualifiedName~{pattern}"

# Specific project
dotnet test tests/TsqlRefine.Rules.Tests -c Release
dotnet test tests/TsqlRefine.Cli.Tests -c Release
dotnet test tests/TsqlRefine.Formatting.Tests -c Release
dotnet test tests/TsqlRefine.Core.Tests -c Release

# Verbose output
dotnet test src/TsqlRefine.sln -c Release --logger "console;verbosity=detailed"
```

## Test Projects

| Project | Path |
|---------|------|
| Rules | `tests/TsqlRefine.Rules.Tests` |
| Cli | `tests/TsqlRefine.Cli.Tests` |
| Formatting | `tests/TsqlRefine.Formatting.Tests` |
| Core | `tests/TsqlRefine.Core.Tests` |

## Output

Report passed/failed counts, show failure details with test name and error message.

Exit code 0 = all passed, 1 = failures.
