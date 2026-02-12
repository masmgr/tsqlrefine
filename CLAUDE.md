# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## Project Overview

**tsqlrefine** is a SQL Server/T-SQL linter, static analyzer, and formatter written in C#. It provides:
- Lint and static analysis for T-SQL code (SQL Server 2012+)
- Minimal SQL formatting (keyword casing, whitespace normalization)
- Plugin system for custom rules
- CLI tool and library for integration

Key patterns: most rules use ScriptDOM AST (preferred over token-based), helpers live in organized subdirectories under `Helpers/`, and autofix logic uses shared helper classes. When refactoring rules, follow the AST-based pattern used by other rules in the codebase.

## Workflow

After any refactoring or code changes, always run the full test suite (all ~1600 tests) before committing. Never commit with failing tests.

## Refactoring Checklist

When moving/renaming files or reorganizing directories, always update namespaces AND using statements in both the main project and the test project. Build before running tests to catch missing references early.

## Testing

When adding new tests, double-check expected error counts and assertion values against the actual rule behavior. Run the specific new tests first before running the full suite.

## Performance Conventions

Prefer `FrozenSet`/`FrozenDictionary` over `HashSet`/`Dictionary` for static lookup collections. Use `StringBuilder` for string concatenation in hot paths. Cache repeated computations.

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
├── TsqlRefine.Cli/           # Command-line interface
└── TsqlRefine.DebugTool/     # Development/debugging utility
```

**Dependency flow**: `Cli` → `Core`/`Formatting`/`PluginHost`/`Rules` → `PluginSdk`

## Configuration

### tsqlrefine.json

```json
{
  "compatLevel": 150,
  "preset": "recommended",
  "plugins": [
    { "path": "plugins/custom.dll", "enabled": true }
  ]
}
```

**Built-in presets** (via `"preset"` field or `--preset` CLI option):
- `recommended`: Balanced production use (58 rules)
- `strict`: Maximum enforcement including style (97 rules)
- `strict-logic`: Comprehensive correctness without cosmetic rules (74 rules)
- `pragmatic`: Production-ready minimum (39 rules)
- `security-only`: Security and critical safety (13 rules)

For custom rulesets, use the `"ruleset"` field with a path to a custom JSON file instead.

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
| 2 | Analysis error |
| 3 | Config error |
| 4 | Fatal error |

## Documentation

- [docs/cli.md](docs/cli.md): CLI specification
- [docs/configuration.md](docs/configuration.md): Configuration format
- [docs/formatting.md](docs/formatting.md): Formatting options
- [docs/granular-casing.md](docs/granular-casing.md): Granular element casing
- [docs/inline-disable.md](docs/inline-disable.md): Inline disable comments
- [docs/plugin-api.md](docs/plugin-api.md): Plugin API contract
- [docs/release.md](docs/release.md): Release procedures
- [docs/Rules/README.md](docs/Rules/README.md): Rules overview and guide
- [docs/Rules/REFERENCE.md](docs/Rules/REFERENCE.md): Full rule reference (auto-generated)

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
