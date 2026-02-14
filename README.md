# tsqlrefine

[![CI](https://github.com/masmgr/tsqlrefine/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/masmgr/tsqlrefine/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/TsqlRefine)](https://www.nuget.org/packages/TsqlRefine)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

T-SQL linter, auto-fixer, and formatter for SQL Server.

> Note: This project is currently in early development (pre-1.0). Breaking changes are expected.

## Quickstart

```bash
# Install
dotnet tool install --global TsqlRefine

# Lint SQL files (warnings and errors are displayed immediately)
tsqlrefine lint path/to/your-sql-files/
```

Generate a configuration file for your project:

```bash
tsqlrefine init
```

This creates `tsqlrefine.json` with sensible defaults:

```json
{
  "compatLevel": 150,
  "preset": "recommended"
}
```

The `recommended` preset enables 87 rules (out of 130 total). See [Preset Rulesets](#preset-rulesets) for other options.

For CI pipelines, use JSON output and exit codes:

```bash
tsqlrefine lint --output json src/**/*.sql
# Exit code 1 = violations found (fails the build)
```

See [CI Integration Guide](docs/ci-integration.md) for full GitHub Actions / Azure Pipelines / GitLab CI examples.

## Features

T-SQL Refine catches problems **before execution** using static analysis on the SQL script alone — no database connection required. Designed for CI/CD pipelines and offline validation.

### Lint - Static Analysis

Detects issues in T-SQL code. Includes 130 built-in rules covering security, correctness, performance, and coding conventions.

Each rule is classified by severity:

- **Error** — Likely to cause runtime failures or data corruption
- **Warning** — Valid SQL but risky or discouraged patterns
- **Information** — Style or maintainability recommendations

This allows teams to gradually enforce stricter rules without blocking development.

```bash
# Lint a file
tsqlrefine lint path/to/file.sql

# Lint a directory recursively
tsqlrefine lint path/to/dir

# Lint from stdin
echo "SELECT * FROM users;" | tsqlrefine lint --stdin

# Output as JSON (for CI integration)
tsqlrefine lint --output json path/to/file.sql
```

**Example output:**

```
path/to/file.sql:3:1: warning avoid-select-star: Avoid SELECT *; explicitly list columns
path/to/file.sql:7:5: error missing-where-clause: UPDATE/DELETE without WHERE clause
```

### Fix - Auto-fix

Automatically fixes detected issues. Rules with `fixable: true` can be auto-fixed.

> **Safe by design:** Auto-fix is applied only to rules explicitly marked as fixable. All fixes are deterministic and syntax-aware — they never produce invalid SQL. Use dry-run mode (the default) to preview changes before writing.

```bash
# Preview fixes (dry run)
tsqlrefine fix path/to/file.sql

# Apply fixes to files
tsqlrefine fix --write path/to/file.sql

# Fix all .sql files in a directory
tsqlrefine fix --write path/to/dir
```

**Auto-fix examples:**

| Before | After |
|--------|-------|
| `select * from users` | `SELECT * FROM users` |
| `IF @x = NULL` | `IF @x IS NULL` |
| `EXEC('SELECT ...')` | `EXEC sp_executesql N'SELECT ...'` |

### Format - Code Formatting

Formats T-SQL code to a consistent style. Respects `.editorconfig` indentation settings.

```bash
# Print formatted output to stdout
tsqlrefine format path/to/file.sql

# Format files in-place
tsqlrefine format --write path/to/file.sql

# Format all .sql files in a directory
tsqlrefine format --write path/to/dir
```

**Formatting features:**

- Keyword uppercasing (`select` → `SELECT`)
- Consistent indentation
- Whitespace normalization
- Trailing whitespace removal

## Installation

### .NET Global Tool (Recommended)

```bash
# Install
dotnet tool install --global TsqlRefine

# Update
dotnet tool update --global TsqlRefine

# Uninstall
dotnet tool uninstall --global TsqlRefine
```

### Local Tool (Project-specific)

```bash
dotnet new tool-manifest
dotnet tool install TsqlRefine
dotnet tsqlrefine --help
```

### From Source

```bash
git clone https://github.com/masmgr/tsqlrefine.git
cd tsqlrefine
dotnet build src/TsqlRefine.sln -c Release
```

## Configuration

### Generate Config Files

```bash
tsqlrefine init
```

Creates the following files:

- `tsqlrefine.json` - Tool configuration
- `tsqlrefine.ignore` - Exclusion patterns

### tsqlrefine.json

```json
{
  "compatLevel": 150,
  "preset": "recommended",
  "plugins": []
}
```

### Preset Rulesets

| Preset | Rules | Use Case |
|--------|-------|----------|
| `security-only` | 14 | Security vulnerabilities and critical safety |
| `pragmatic` | 43 | Production-ready minimum for legacy codebases |
| `recommended` | 87 | Balanced for production (default) |
| `strict-logic` | 107 | Comprehensive correctness without cosmetic rules |
| `strict` | 130 | Maximum enforcement including style |

Each preset is a strict superset of the one below: `security-only` ⊂ `pragmatic` ⊂ `recommended` ⊂ `strict-logic` ⊂ `strict`

```bash
tsqlrefine lint --preset strict path/to/file.sql
```

### .editorconfig

The `format` command respects indentation settings:

```ini
[*.sql]
indent_style = space
indent_size = 4
```

## Team Adoption Guide

Gradually introduce tsqlrefine to your team by starting strict on critical issues and expanding over time.

### Step 1: Security & Safety (Block PRs)

Start with the `security-only` preset. These 14 rules catch SQL injection, dangerous procedures, and accidental mass UPDATE/DELETE — issues that should never reach production.

```bash
tsqlrefine lint --preset security-only src/**/*.sql
```

### Step 2: Correctness (Expand Coverage)

Move to `pragmatic` to add 29 correctness rules: duplicate aliases, column count mismatches, undefined references, and other bugs that cause runtime failures.

```bash
tsqlrefine lint --preset pragmatic src/**/*.sql
```

### Step 3: Best Practices (Default)

Adopt `recommended` (the default preset) for full semantic analysis, performance warnings, and transaction handling best practices.

```bash
tsqlrefine lint --preset recommended src/**/*.sql
```

### Step 4: Full Enforcement (Optional)

For teams wanting maximum consistency, `strict` adds naming conventions, formatting rules, and cosmetic checks.

```bash
tsqlrefine lint --preset strict src/**/*.sql
```

**Suggested timeline**: Start at `security-only` for 1-2 sprints, then advance one level per sprint. Use per-rule severity overrides to promote specific rules to `error` as needed.

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success (no violations) |
| 1 | Rule violations found |
| 2 | Parse error |
| 3 | Config error |
| 4 | Runtime exception |

## Rules and Plugins

```bash
# List built-in rules
tsqlrefine list-rules

# List loaded plugins
tsqlrefine list-plugins
```

See [docs/Rules/README.md](docs/Rules/README.md) for a rules overview, or [docs/Rules/REFERENCE.md](docs/Rules/REFERENCE.md) for the full rule reference.

## Documentation

- [CLI Specification](docs/cli.md)
- [Configuration](docs/configuration.md)
- [CI Integration Guide](docs/ci-integration.md)
- [Editor Integration](docs/editor-integration.md)
- [Formatting Options](docs/formatting.md)
- [Plugin API](docs/plugin-api.md)
- [Rules Overview](docs/Rules/README.md)
- [Rule Reference](docs/Rules/REFERENCE.md)

## License

MIT License - see [LICENSE](LICENSE)
