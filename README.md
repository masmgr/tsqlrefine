# tsqlrefine

T-SQL linter, auto-fixer, and formatter for SQL Server.

> Note: This project is currently in early development (pre-1.0). Breaking changes are expected.

## Why T-SQL Refine?

Managing T-SQL queries in a Git repository without a live database connection introduces unique risks:

- Queries can silently break during merges or refactoring, with errors surfacing only at execution time
- Stored procedures and views may reference dropped columns or mismatched types that go unnoticed until deployment
- Inconsistent formatting creates noisy diffs and slows code review

T-SQL Refine catches these problems **before execution** using static analysis on the SQL script alone — no database connection required. It is designed for CI/CD pipelines and offline validation, so your team can enforce quality gates on every pull request.

## Features

### Lint - Static Analysis

Detects issues in T-SQL code. Includes 89 built-in rules covering security, performance, and coding conventions.

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
git clone https://github.com/user/tsqlrefine.git
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
| `recommended` | 58 | Balanced for production (default) |
| `strict` | 97 | Maximum enforcement including style |
| `strict-logic` | 74 | Comprehensive correctness without cosmetic rules |
| `pragmatic` | 34 | Production-ready minimum for legacy codebases |
| `security-only` | 13 | Security vulnerabilities and critical safety |

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
- [Formatting Options](docs/formatting.md)
- [Plugin API](docs/plugin-api.md)
- [Rules Overview](docs/Rules/README.md)
- [Rule Reference](docs/Rules/REFERENCE.md)

## License

MIT License - see [LICENSE](LICENSE)
