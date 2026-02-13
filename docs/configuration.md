# Configuration

This document describes how to configure `tsqlrefine` using:

- `tsqlrefine.json` (main config file)
- `tsqlrefine.ignore` (ignore patterns file)
- A ruleset JSON file (enables/disables rules)

Schemas are available under `schemas/`:

- `schemas/tsqlrefine.schema.json`
- `schemas/ruleset.schema.json`

## Configuration File Discovery

tsqlrefine searches for configuration files (`tsqlrefine.json` and `tsqlrefine.ignore`) in the following order:

| Priority | Location | Description |
|----------|----------|-------------|
| 1 (highest) | CLI argument | `--config` / `--ignorelist` explicit path |
| 2 | `{CWD}/tsqlrefine.json` | Current directory (legacy, backward-compatible) |
| 3 | `{CWD}/.tsqlrefine/` | Current directory `.tsqlrefine/` subdirectory |
| 4 | `~/.tsqlrefine/` | Home directory `.tsqlrefine/` subdirectory |
| 5 (lowest) | Default | Built-in defaults (no file) |

The first file found wins. Configuration files are **not** merged across locations.

Use `tsqlrefine init` to create a `.tsqlrefine/` directory with default configuration files, or `tsqlrefine init --global` to create them in your home directory.

## tsqlrefine.json

`tsqlrefine.json` supports these top-level properties:

- `compatLevel` (integer): SQL Server compatibility level used by the parser (`100`, `110`, `120`, `150`, `160`).
- `preset` (string): name of a built-in preset ruleset (e.g. `"recommended"`, `"strict"`).
- `ruleset` (string): path to a custom ruleset file. Can be relative to the working directory or absolute. For built-in presets, use `preset` instead.
- `plugins` (array): plugin DLLs to load (optional).
- `rules` (object): per-rule severity overrides (optional). See [Per-Rule Configuration](#per-rule-configuration).

> **Note**: If both `preset` and `ruleset` are specified, `preset` takes precedence. The `--preset` CLI option overrides both.

Example:

```json
{
  "compatLevel": 150,
  "preset": "recommended",
  "plugins": [
    { "path": "plugins/custom-rules.dll", "enabled": true }
  ],
  "rules": {
    "avoid-select-star": "none",
    "dml-without-where": "error"
  },
  "formatting": {
    "indentStyle": "spaces",
    "indentSize": 4,
    "keywordCasing": "upper",
    "functionCasing": "upper",
    "dataTypeCasing": "lower",
    "schemaCasing": "none",
    "tableCasing": "none",
    "columnCasing": "none",
    "variableCasing": "lower"
  }
}
```

### Formatting Configuration

The optional `formatting` section allows you to customize SQL formatting behavior:

```json
{
  "formatting": {
    "indentStyle": "spaces",
    "indentSize": 4,
    "keywordCasing": "upper",
    "functionCasing": "upper",
    "dataTypeCasing": "lower",
    "schemaCasing": "none",
    "tableCasing": "none",
    "columnCasing": "none",
    "variableCasing": "lower",
    "systemTableCasing": "lower",
    "storedProcedureCasing": "none",
    "userDefinedFunctionCasing": "none",
    "commaStyle": "trailing",
    "maxLineLength": 0,
    "insertFinalNewline": true,
    "trimTrailingWhitespace": true,
    "normalizeInlineSpacing": true,
    "normalizeOperatorSpacing": true,
    "normalizeKeywordSpacing": true,
    "normalizeFunctionSpacing": true,
    "lineEnding": "auto",
    "maxConsecutiveBlankLines": 0,
    "trimLeadingBlankLines": true
  }
}
```

**Available options:**

**Indentation:**
- `indentStyle` (string): `"spaces"` or `"tabs"`. Default: `"spaces"`
- `indentSize` (integer): Number of spaces per indent level (for spaces) or tab width (for tabs). Default: `4`

**Casing** (values: `"none"`, `"upper"`, `"lower"`, `"pascal"`):
- `keywordCasing`: SQL keywords (SELECT, FROM, etc.). Default: `"upper"`
- `functionCasing`: Built-in functions (COUNT, SUM, etc.). Default: `"upper"`
- `dataTypeCasing`: Data types (INT, VARCHAR, etc.). Default: `"lower"`
- `schemaCasing`: Schema names (dbo, sys, etc.). Default: `"none"`
- `tableCasing`: Table names and aliases. Default: `"none"`
- `columnCasing`: Column names and aliases. Default: `"none"`
- `variableCasing`: Variables (@var, @@rowcount). Default: `"lower"`
- `systemTableCasing`: System tables (sys.*, information_schema.*). Default: `"lower"`
- `storedProcedureCasing`: Stored procedure names. Default: `"none"`
- `userDefinedFunctionCasing`: User-defined function names. Default: `"none"`

> **Warning**: Changing casing for schema/table/column may break queries in case-sensitive collation environments.

**Comma and Line:**
- `commaStyle` (string): `"trailing"` or `"leading"`. Default: `"trailing"`
- `maxLineLength` (integer): Maximum line length (0 = no limit). Default: `0`
- `lineEnding` (string): `"auto"`, `"lf"`, or `"crlf"`. Default: `"auto"`

**Whitespace:**
- `insertFinalNewline` (boolean): Insert final newline at end of file. Default: `true`
- `trimTrailingWhitespace` (boolean): Trim trailing whitespace on lines. Default: `true`
- `normalizeInlineSpacing` (boolean): Normalize inline spacing (space after commas). Default: `true`
- `normalizeOperatorSpacing` (boolean): Normalize operator spacing (space around binary operators). Default: `true`
- `normalizeKeywordSpacing` (boolean): Normalize compound keyword spacing. Default: `true`
- `normalizeFunctionSpacing` (boolean): Remove space between function name and `(`. Default: `true`

**Blank Lines:**
- `maxConsecutiveBlankLines` (integer): Maximum consecutive blank lines (0 = no limit). Default: `0`
- `trimLeadingBlankLines` (boolean): Remove leading blank lines at start of file. Default: `true`

**Priority order:**

1. CLI arguments (`--indent-style`, `--indent-size`)
2. `.editorconfig` settings (for indentation only)
3. `tsqlrefine.json` formatting section
4. Built-in defaults

## Ruleset file

A ruleset file declares a **whitelist** of rules to enable. Only rules listed in the file are active; all other rules are disabled.

Each rule entry uses a `severity` field to control severity:

| Value | Enabled | Severity |
|-------|---------|----------|
| `"error"` | Yes | Error |
| `"warning"` | Yes | Warning |
| `"info"` | Yes | Information |
| `"inherit"` | Yes | Rule's default severity |
| `"none"` | No | — |

When `severity` is omitted, it defaults to `"inherit"` (enabled with the rule's default severity).

Example `custom-ruleset.json`:

```json
{
  "rules": [
    { "id": "avoid-null-comparison" },
    { "id": "dml-without-where", "severity": "error" }
  ]
}
```

In this example, only `avoid-null-comparison` and `dml-without-where` are enabled. All other rules are disabled.

## Preset rulesets

tsqlrefine includes built-in preset rulesets:

| Preset | Rules | Description |
|--------|-------|-------------|
| `recommended` | 87 | Balanced production use with semantic analysis (default) |
| `strict` | 130 | Maximum enforcement including all style/cosmetic rules |
| `strict-logic` | 107 | Comprehensive correctness and semantic analysis without cosmetic style rules |
| `pragmatic` | 49 | Production-ready minimum focusing on safety and critical issues |
| `security-only` | 14 | Security vulnerabilities and critical safety only |

Use the `preset` property in `tsqlrefine.json` or the `--preset` CLI option:

```json
{
  "preset": "recommended"
}
```

```bash
tsqlrefine lint --preset strict file.sql
```

Presets form a strict inclusion hierarchy — each higher preset is a superset of the one below:

```
security-only ⊂ pragmatic ⊂ recommended ⊂ strict-logic ⊂ strict
```

This means upgrading from one preset to the next only **adds** rules; it never removes rules you were already checking.

**Choosing a preset:**
- Start with `recommended` for most projects — it provides balanced production-ready linting
- Use `pragmatic` for minimal enforcement in legacy codebases or when first introducing linting
- Use `strict-logic` for comprehensive correctness checking without style/cosmetic noise
- Use `strict` for maximum enforcement when you want both logic and style consistency
- Use `security-only` for security-focused code review or CI gates

## Per-Rule Configuration

The `rules` property in `tsqlrefine.json` lets you override individual rule severity on top of the selected preset or ruleset.

```json
{
  "preset": "recommended",
  "rules": {
    "avoid-select-star": "none",
    "dml-without-where": "error",
    "avoid-nolock": "warning"
  }
}
```

Keys are rule IDs. Values are severity levels: `"error"`, `"warning"`, `"info"`, `"inherit"`, or `"none"`.

| Value | Effect |
|-------|--------|
| `"error"` | Enable with Error severity |
| `"warning"` | Enable with Warning severity |
| `"info"` | Enable with Information severity |
| `"inherit"` | Enable with the rule's default severity |
| `"none"` | Disable the rule |

### Resolution order

Severity is resolved in this order (later overrides earlier):

1. **Rule default** — severity defined in the rule code
2. **Preset / ruleset file** — `severity` field in the ruleset entry
3. **tsqlrefine.json `rules`** — per-rule overrides (highest priority)

