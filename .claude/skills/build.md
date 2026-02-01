---
name: build
description: Build tsqlrefine solution. Use when: compiling the project, checking for build errors, verifying code compiles, or running build + tests together. Supports Release/Debug configurations.
---

# Build

Build with `dotnet build`.

## Commands

```powershell
# Build Release
dotnet build src/TsqlRefine.sln -c Release

# Build Debug
dotnet build src/TsqlRefine.sln -c Debug

# Warnings as errors
dotnet build src/TsqlRefine.sln -c Release -warnaserror

# Build + test (full verification)
dotnet build src/TsqlRefine.sln -c Release && dotnet test src/TsqlRefine.sln -c Release

# Clean build
dotnet clean src/TsqlRefine.sln -c Release && dotnet build src/TsqlRefine.sln -c Release
```

## Projects

| Project | Type |
|---------|------|
| TsqlRefine.PluginSdk | Library (core interfaces) |
| TsqlRefine.Core | Library (analysis engine) |
| TsqlRefine.Rules | Library (linting rules) |
| TsqlRefine.Formatting | Library (formatter) |
| TsqlRefine.PluginHost | Library (plugin loading) |
| TsqlRefine.Cli | Executable (CLI tool) |

## Output

Report build status, warning count, error details if any.

Exit code 0 = success, 1 = failure.
