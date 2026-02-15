# CLAUDE.md

## Project Overview

**tsqlrefine** is a SQL Server/T-SQL linter, static analyzer, and formatter written in C#. It provides:
- Lint and static analysis for T-SQL code (SQL Server 2012+)
- Minimal SQL formatting (keyword casing, whitespace normalization)
- Plugin system for custom rules
- CLI tool and library for integration

Key patterns: most rules use ScriptDOM AST (preferred over token-based), helpers live in organized subdirectories under `Helpers/`, and autofix logic uses shared helper classes. When refactoring rules, follow the AST-based pattern used by other rules in the codebase.

## Workflow

- Run the full test suite (~1600 tests) before committing. Never commit with failing tests.
- When moving/renaming files, update namespaces AND using statements in both main and test projects. Build before running tests to catch missing references early.
- When adding tests, double-check expected error counts against actual rule behavior. Run new tests first before the full suite.

## Performance Conventions

Prefer `FrozenSet`/`FrozenDictionary` over `HashSet`/`Dictionary` for static lookup collections. Use `StringBuilder` for string concatenation in hot paths. Cache repeated computations.

## CLI Usage

```powershell
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
- `recommended`: Balanced production use (87 rules)
- `strict`: Maximum enforcement including style (130 rules)
- `strict-logic`: Comprehensive correctness without cosmetic rules (107 rules)
- `pragmatic`: Production-ready minimum (43 rules)
- `security-only`: Security and critical safety (14 rules)

For custom rulesets, use the `"ruleset"` field with a name (resolved from `.tsqlrefine/rulesets/`) or a file path.

Configuration files are discovered from `.tsqlrefine/` directories (project-level then home-level). Use `--config` for explicit paths.

### .editorconfig

Format command respects `.editorconfig` for indentation:
```ini
[*.sql]
indent_style = spaces
indent_size = 4
```

## Documentation

- [docs/cli.md](docs/cli.md): CLI specification
- [docs/configuration.md](docs/configuration.md): Configuration format
- [docs/Rules/REFERENCE.md](docs/Rules/REFERENCE.md): Full rule reference (auto-generated)

See also: `docs/formatting.md`, `docs/granular-casing.md`, `docs/inline-disable.md`, `docs/plugin-api.md`, `docs/release.md`.

## Development Guidelines

Path-specific rules auto-load via `.claude/rules/` YAML frontmatter:

| File | Applies To |
|------|------------|
| [project-conventions.md](.claude/rules/project-conventions.md) | All code (global conventions) |
| [rules-development.md](.claude/rules/rules-development.md) | `src/TsqlRefine.Rules/**` |
| [formatting-development.md](.claude/rules/formatting-development.md) | `src/TsqlRefine.Formatting/**` |
| [cli-development.md](.claude/rules/cli-development.md) | `src/TsqlRefine.Cli/**` |
| [core-development.md](.claude/rules/core-development.md) | `src/TsqlRefine.Core/**`, `src/TsqlRefine.PluginSdk/**` |
| [plugin-development.md](.claude/rules/plugin-development.md) | `src/TsqlRefine.PluginHost/**` |
| [testing-patterns.md](.claude/rules/testing-patterns.md) | `tests/**` |
