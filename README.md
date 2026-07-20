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

The `recommended` preset enables 112 rules (out of 169 total). See [Preset Rulesets](#preset-rulesets) for other options.

For CI pipelines, use JSON output and exit codes:

```bash
tsqlrefine lint --output json src/**/*.sql
# Exit code 1 = violations found (fails the build)
```

See [CI Integration Guide](docs/ci-integration.md) for full GitHub Actions / Azure Pipelines / GitLab CI examples.

Existing codebases can adopt linting incrementally with a diagnostic baseline, while CI systems
can consume SARIF directly:

```bash
# Record existing diagnostics, then fail only on new diagnostics
tsqlrefine baseline create --output .tsqlrefine/baseline.json src
tsqlrefine lint --baseline .tsqlrefine/baseline.json src

# GitHub Code Scanning / Azure DevOps compatible output
tsqlrefine lint --output sarif src > tsqlrefine.sarif

# Save a self-contained quality report as a CI artifact
tsqlrefine report --output-format html --output tsqlrefine-report.html src

# On pull requests, report diagnostics only on changed lines
tsqlrefine lint --changed-only --base-ref origin/main src
```

The baseline path can also be stored as `"baseline": ".tsqlrefine/baseline.json"` in
`tsqlrefine.json`. Use `baseline trim` after fixing existing findings.

Changed-only lint combines committed, staged, working-tree, and untracked changes. In environments
without Git, pass a versioned changed-lines JSON file with `--changed-lines-from`.

The `report` command summarizes diagnostics by category, rule, and file and ranks complex SQL
objects using structural metrics. When a baseline is supplied, it also reports new, frozen, and
resolved findings:

```bash
tsqlrefine report --baseline .tsqlrefine/baseline.json --output report.json src
```

## Features

T-SQL Refine catches problems **before execution** using static analysis on the SQL script alone — no database connection required. Designed for CI/CD pipelines and offline validation.

### Lint - Static Analysis

Detects issues in T-SQL code. Includes 169 built-in rules covering security, correctness, performance, and coding conventions.

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

# Output SARIF 2.1.0
tsqlrefine lint --output sarif path/to/file.sql
```

**Example output:**

```
path/to/file.sql:3:1: warning avoid-select-star: Avoid SELECT *; explicitly list columns
path/to/file.sql:7:5: error missing-where-clause: UPDATE/DELETE without WHERE clause
```

### Fix - Auto-fix

Automatically fixes detected issues. Rules with `fixable: true` can be auto-fixed.

> **Safe by design:** Auto-fix is applied only to rules explicitly marked as fixable. All fixes are deterministic and syntax-aware — they never produce invalid SQL. Use `--output json` to preview changes without writing.

```bash
# Apply fixes to a file (writes back automatically)
tsqlrefine fix path/to/file.sql

# Fix all .sql files in a directory
tsqlrefine fix path/to/dir

# Preview fixes without writing (dry run)
tsqlrefine fix --output json path/to/file.sql
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
# Format a file in-place (writes back automatically)
tsqlrefine format path/to/file.sql

# Format all .sql files in a directory
tsqlrefine format path/to/dir

# Print formatted output to stdout instead of writing
"select * from t;" | tsqlrefine format --stdin
```

**Formatting features:**

- Keyword uppercasing (`select` → `SELECT`)
- Consistent indentation
- Whitespace normalization
- Trailing whitespace removal

### Schema Snapshots

Schema-aware rules can use a snapshot generated from SQL Server. To keep credentials out
of process listings, shell history, and CI logs, prefer the environment variable over a
password-bearing command-line argument:

```bash
export TSQLREFINE_CONNECTION_STRING='Server=localhost;Database=app;User ID=...;Password=...'
tsqlrefine schema snapshot --output .tsqlrefine/schema.json
```

`--connection-string` remains available and takes precedence over
`TSQLREFINE_CONNECTION_STRING` when both are supplied.

For cross-object analysis, collect procedure, function, and view signatures from the SQL source:

```bash
tsqlrefine schema collect-objects --output .tsqlrefine/objects.json sql/
tsqlrefine lint --objects-catalog .tsqlrefine/objects.json sql/
```

`schema build --output-dir .tsqlrefine/schema sql/` generates `schema.json`, `relations.json`,
and `objects.json` together.

Use the collected object catalog for impact analysis or dependency graph export:

```bash
tsqlrefine analyze impact --table dbo.Users --column Email --catalog .tsqlrefine/schema/objects.json
tsqlrefine analyze graph --catalog .tsqlrefine/schema/objects.json --format dot --output dependencies.dot
```

Impact analysis follows reverse dependencies transitively, making it useful before table or column
changes. Graph export supports versioned JSON and Graphviz DOT.

Compare two snapshots to detect schema drift. Supplying the object catalog adds direct and
transitive dependent procedures, functions, and views to each breaking change:

```bash
tsqlrefine schema diff --from schema-main.json --to schema-candidate.json \
  --catalog .tsqlrefine/schema/objects.json --output schema-diff.json
```

`schema diff` returns exit code 1 when it finds a breaking removal, type change, or
nullable-to-`NOT NULL` change, so it can be used as a CI quality gate.

## Installation

### .NET Global Tool (Recommended)

Available on [NuGet](https://www.nuget.org/packages/TsqlRefine).

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

### VS Code Extension

Install the [tsqlrefine extension](https://marketplace.visualstudio.com/items?itemName=masmgr.tsqlrefine) from the VS Code Marketplace for integrated linting, auto-fix, and formatting directly in the editor.

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
| `security-only` | 17 | Security vulnerabilities and critical safety |
| `pragmatic` | 52 | Production-ready minimum for legacy codebases |
| `recommended` | 112 | Balanced for production (default) |
| `strict-logic` | 146 | Comprehensive correctness without cosmetic rules |
| `strict` | 169 | Maximum enforcement including style |

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

Start with the `security-only` preset. These 17 rules catch SQL injection, exposed passwords, broken error propagation, dangerous procedures, and accidental mass UPDATE/DELETE — issues that should never reach production.

```bash
tsqlrefine lint --preset security-only src/**/*.sql
```

### Step 2: Correctness (Expand Coverage)

Move to `pragmatic` to add 35 rules: duplicate aliases, column count mismatches, EXEC signature validation, path-sensitive transaction checks, undefined references, and other bugs that cause runtime failures.

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
