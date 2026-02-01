# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## Project Overview

**tsqlrefine** is a SQL Server/T-SQL linter, static analyzer, and formatter written in C#. It provides:
- Lint and static analysis for T-SQL code (SQL Server 2012+)
- Minimal SQL formatting (keyword casing, whitespace normalization)
- Plugin system for custom rules
- CLI tool and library for integration

## Quick Reference Commands

```powershell
# Build
dotnet build src/TsqlRefine.sln -c Release

# Test
dotnet test src/TsqlRefine.sln -c Release

# Lint SQL
dotnet run --project src/TsqlRefine.Cli -c Release -- lint file.sql

# Format SQL
dotnet run --project src/TsqlRefine.Cli -c Release -- format file.sql

# Auto-fix
dotnet run --project src/TsqlRefine.Cli -c Release -- fix file.sql

# Lint from stdin
echo "SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin
```

## Architecture Overview

```
src/
├── TsqlRefine.PluginSdk/     # Contracts and interfaces (foundation, zero dependencies)
├── TsqlRefine.Core/          # Analysis engine and tokenizer
├── TsqlRefine.Rules/         # Built-in rules and helper classes
├── TsqlRefine.Formatting/    # SQL formatter
├── TsqlRefine.PluginHost/    # Plugin loading infrastructure
└── TsqlRefine.Cli/           # Command-line interface
```

**Dependency flow**: `Cli` → `Core`/`Formatting`/`PluginHost`/`Rules` → `PluginSdk`

## Configuration

### tsqlrefine.json

```json
{
  "compatLevel": 150,
  "ruleset": "rulesets/recommended.json",
  "plugins": [
    { "path": "plugins/custom.dll", "enabled": true }
  ]
}
```

**Preset rulesets** in `rulesets/`:
- `recommended.json`: Balanced production use (49 rules)
- `strict.json`: Maximum enforcement (86 rules)
- `pragmatic.json`: Production-ready minimum (30 rules)
- `security-only.json`: Security and critical safety (10 rules)

### .editorconfig

Format command respects `.editorconfig` for indentation:
```ini
[*.sql]
indent_style = spaces
indent_size = 4
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success (no violations) |
| 1 | Rule violations found |
| 2 | Parse error |
| 3 | Config error |
| 4 | Runtime exception |

## Documentation

- [docs/cli.md](docs/cli.md): CLI specification
- [docs/configuration.md](docs/configuration.md): Configuration format
- [docs/formatting.md](docs/formatting.md): Formatting options
- [docs/plugin-api.md](docs/plugin-api.md): Plugin API contract
- [docs/Rules/README.md](docs/Rules/README.md): All 86 built-in rules

## Development Guidelines

Path-specific development patterns are in `.claude/rules/`:

| File | Applies To |
|------|------------|
| [project-conventions.md](.claude/rules/project-conventions.md) | All code (global conventions) |
| [rules-development.md](.claude/rules/rules-development.md) | `src/TsqlRefine.Rules/**` |
| [formatting-development.md](.claude/rules/formatting-development.md) | `src/TsqlRefine.Formatting/**` |
| [cli-development.md](.claude/rules/cli-development.md) | `src/TsqlRefine.Cli/**` |
| [core-development.md](.claude/rules/core-development.md) | `src/TsqlRefine.Core/**`, `src/TsqlRefine.PluginSdk/**` |
| [plugin-development.md](.claude/rules/plugin-development.md) | `src/TsqlRefine.PluginHost/**` |
| [testing-patterns.md](.claude/rules/testing-patterns.md) | `tests/**` |

These rules use YAML frontmatter with `paths` field to automatically load context-specific guidance when working with files in those directories.
