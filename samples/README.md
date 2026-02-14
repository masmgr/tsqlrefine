# tsqlrefine Samples

This directory contains sample files demonstrating how to configure and use tsqlrefine.

## Directory Structure

```
samples/
├── ci/                  CI/CD workflow examples
│   ├── github-actions-basic.yml       Minimal GitHub Actions workflow
│   ├── github-actions-advanced.yml    Diff-only linting with JSON artifacts
│   └── README.md
├── editor/              Editor integration examples
│   ├── vscode-tasks.json              VS Code tasks with problem matcher
│   ├── pre-commit-hook.sh             Git pre-commit hook
│   └── README.md
├── configs/             Configuration file examples
│   ├── basic.json              Basic configuration
│   ├── advanced.json           Advanced configuration with plugins
│   ├── minimal.json            Minimal configuration (uses defaults)
│   ├── sql-server-2012.json    SQL Server 2012 compatibility
│   ├── formatting-options.json Complete formatting options example
│   └── tsqlrefine.ignore       File ignore patterns
├── rulesets/            Ruleset examples
│   ├── recommended.json        All rules enabled (recommended)
│   ├── strict.json             All rules enabled (strict enforcement)
│   ├── security-only.json      Only safety/security rules
│   └── custom.json             Custom mix of enabled/disabled rules
├── plugins/             Plugin examples
│   └── custom-rule/            Sample custom rule plugin
│       ├── CustomRule.csproj
│       ├── CustomRuleProvider.cs
│       ├── NoMagicNumbersRule.cs
│       └── README.md
└── sql/                 SQL code samples
    ├── select-star.sql         Simple example
    ├── comprehensive.sql       Comprehensive sample with multiple patterns
    └── rules/                  Individual rule demonstrations
        ├── avoid-select-star.sql
        ├── dml-without-where.sql
        ├── avoid-null-comparison.sql
        ├── require-parentheses-for-mixed-and-or.sql
        ├── avoid-nolock.sql
        ├── require-column-list-for-insert-values.sql
        └── require-column-list-for-insert-select.sql
```

## Quick Start

### Basic Linting

Lint a SQL file:

```bash
# If installed as a global tool
tsqlrefine lint samples/sql/select-star.sql

# Or from the repository source
dotnet run --project src/TsqlRefine.Cli -c Release -- lint samples/sql/select-star.sql
```

### Using a Configuration File

```powershell
# Use basic config
dotnet run --project src/TsqlRefine.Cli -c Release -- lint --config samples/config/tsqlrefine.json samples/sql/comprehensive.sql

# Use SQL Server 2012 compatibility
dotnet run --project src/TsqlRefine.Cli -c Release -- lint --config samples/config/sql-server-2012.json samples/sql/comprehensive.sql
```

### Using a Custom Ruleset

```powershell
# Use recommended ruleset
dotnet run --project src/TsqlRefine.Cli -c Release -- lint --ruleset samples/rulesets/recommended.json samples/sql/comprehensive.sql

# Use security-only rules
dotnet run --project src/TsqlRefine.Cli -c Release -- lint --ruleset samples/rulesets/security-only.json samples/sql/comprehensive.sql
```

### Testing Individual Rules

```powershell
# Test avoid-select-star rule
dotnet run --project src/TsqlRefine.Cli -c Release -- lint samples/sql/rules/avoid-select-star.sql

# Test all rule samples
dotnet run --project src/TsqlRefine.Cli -c Release -- lint samples/sql/rules/*.sql --output json
```

## Configuration Samples

### Basic Configuration ([config/tsqlrefine.json](config/tsqlrefine.json))

```json
{
  "compatLevel": 150,
  "preset": "recommended",
  "plugins": []
}
```

**When to use**: Standard projects targeting SQL Server 2019. Uses the built-in `recommended` preset.

### Advanced Configuration ([config/advanced.json](config/advanced.json))

```json
{
  "compatLevel": 160,
  "ruleset": "samples/rulesets/strict.json",
  "plugins": [
    { "path": "samples/plugins/custom-rule/bin/Release/net10.0/CustomRule.dll", "enabled": true }
  ]
}
```

**When to use**: Projects with custom ruleset files or plugins targeting SQL Server 2022. Uses the `ruleset` field to reference a custom file path.

### Minimal Configuration ([config/minimal.json](config/minimal.json))

```json
{}
```

**When to use**: Use all defaults (compat level 150, all built-in rules enabled)

### SQL Server 2012 Configuration ([config/sql-server-2012.json](config/sql-server-2012.json))

```json
{
  "compatLevel": 110,
  "ruleset": "samples/rulesets/recommended.json"
}
```

**When to use**: Legacy projects with a custom ruleset file targeting SQL Server 2012

### Formatting Options ([configs/formatting-options.json](configs/formatting-options.json))

```json
{
  "compatLevel": 150,
  "formatting": {
    "indentStyle": "spaces",
    "indentSize": 2,
    "keywordCasing": "upper",
    "identifierCasing": "preserve",
    "commaStyle": "trailing",
    "maxLineLength": 120,
    "insertFinalNewline": true,
    "trimTrailingWhitespace": true
  }
}
```

**When to use**: Customize SQL formatting behavior

**Available casing options:**
- `keywordCasing`: `preserve`, `upper` (default), `lower`, `pascal`
- `identifierCasing`: `preserve` (default), `upper`, `lower`, `pascal`, `camel`
- `indentStyle`: `spaces` (default), `tabs`
- `commaStyle`: `trailing` (default), `leading`

## Ruleset Samples

### Recommended Ruleset ([rulesets/recommended.json](rulesets/recommended.json))

All 7 built-in rules enabled. Best for most projects.

**Rules included**:
- avoid-select-star (Performance/Warning)
- dml-without-where (Safety/Error)
- avoid-null-comparison (Correctness/Warning)
- require-parentheses-for-mixed-and-or (Correctness/Warning)
- avoid-nolock (Correctness/Warning)
- require-column-list-for-insert-values (Correctness/Warning)
- require-column-list-for-insert-select (Correctness/Warning)

### Strict Ruleset ([rulesets/strict.json](rulesets/strict.json))

Identical to recommended but with stricter enforcement. Use for high-quality code standards.

### Security-Only Ruleset ([rulesets/security-only.json](rulesets/security-only.json))

Only safety-critical rules enabled:
- dml-without-where (prevents accidental mass data deletion/updates)

**When to use**: CI/CD pipelines that only block on critical issues

### Custom Ruleset ([rulesets/custom.json](rulesets/custom.json))

Example of selectively enabling/disabling rules. Customize this for your project's needs.

## SQL Samples

### Individual Rule Samples ([sql/rules/](sql/rules/))

Each file demonstrates one specific rule with both violating and compliant examples:

- **avoid-select-star.sql** - SELECT * usage
- **dml-without-where.sql** - UPDATE/DELETE without WHERE
- **avoid-null-comparison.sql** - NULL comparisons with = or <>
- **require-parentheses-for-mixed-and-or.sql** - AND/OR precedence
- **avoid-nolock.sql** - NOLOCK hint usage
- **require-column-list-for-insert-values.sql** - INSERT VALUES without columns
- **require-column-list-for-insert-select.sql** - INSERT SELECT without columns

### Comprehensive Sample ([sql/comprehensive.sql](sql/comprehensive.sql))

A realistic SQL file containing:
- Bad practices that trigger multiple rules
- Good practices demonstrating compliance
- Complex queries (JOINs, CTEs, stored procedures)

**Use this to test**:
```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- lint samples/sql/comprehensive.sql --output json
```

## Plugin Sample

The [plugins/custom-rule/](plugins/custom-rule/) directory contains a complete example of a custom rule plugin.

### Building the Plugin

```powershell
# From samples/plugins/custom-rule
dotnet build -c Release

# Output: bin/Release/net10.0/CustomRule.dll
```

### Using the Plugin

1. Build the plugin (see above)
2. Reference it in your config:

```json
{
  "plugins": [
    { "path": "samples/plugins/custom-rule/bin/Release/net10.0/CustomRule.dll", "enabled": true }
  ]
}
```

3. Run linting:

```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- lint --config samples/config/advanced.json samples/sql/comprehensive.sql
```

### Creating Your Own Plugin

See [plugins/custom-rule/README.md](plugins/custom-rule/README.md) for detailed instructions on creating custom rules.

## Compatibility Levels

tsqlrefine supports the following SQL Server compatibility levels:

| CompatLevel | SQL Server Version |
|-------------|-------------------|
| 100         | SQL Server 2008   |
| 110         | SQL Server 2012   |
| 120         | SQL Server 2014   |
| 150         | SQL Server 2019   |
| 160         | SQL Server 2022   |

**Default**: 150 (SQL Server 2019)

## Output Formats

### Text Output (default)

```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- lint samples/sql/select-star.sql
```

Example output:
```
samples/sql/select-star.sql:5:1: warning: Avoid SELECT * in queries. [avoid-select-star]
Found 1 issue(s)
```

### JSON Output

```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- lint samples/sql/comprehensive.sql --output json
```

Example output:
```json
{
  "tool": "tsqlrefine",
  "version": "0.1.0",
  "command": "lint",
  "files": [
    {
      "filePath": "samples/sql/comprehensive.sql",
      "diagnostics": [
        {
          "range": {
            "start": { "line": 8, "character": 0 },
            "end": { "line": 8, "character": 32 }
          },
          "message": "Avoid SELECT * in queries.",
          "severity": 2,
          "code": "avoid-select-star",
          "source": "tsqlrefine",
          "data": {
            "ruleId": "avoid-select-star",
            "category": "Performance",
            "fixable": false
          }
        }
      ]
    }
  ]
}
```

## Formatting

tsqlrefine also includes a SQL formatter for keyword casing and whitespace normalization:

```powershell
# Format to stdout
dotnet run --project src/TsqlRefine.Cli -c Release -- format samples/sql/select-star.sql

# Format in place
dotnet run --project src/TsqlRefine.Cli -c Release -- format samples/sql/select-star.sql --write
```

## Further Reading

- [CLI Documentation](../docs/cli.md) - Complete CLI reference
- [CI Integration Guide](../docs/ci-integration.md) - Pipeline setup for GitHub Actions, Azure, GitLab
- [Editor Integration](../docs/editor-integration.md) - VS Code tasks, pre-commit hooks
- [Configuration](../docs/configuration.md) - Configuration format and options
- [Rules Overview](../docs/Rules/README.md) - Rule categories and presets
- [Rule Reference](../docs/Rules/REFERENCE.md) - All 130 rules with details
- [Plugin API](../docs/plugin-api.md) - Plugin development guide

## Contributing

Found an issue or have a suggestion? Please report it at:
https://github.com/anthropics/tsqlrefine/issues
