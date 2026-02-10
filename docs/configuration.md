# Configuration

This document describes how to configure `tsqlrefine` using:

- `tsqlrefine.json` (main config file)
- A ruleset JSON file (enables/disables rules)

Schemas are available under `schemas/`:

- `schemas/tsqlrefine.schema.json`
- `schemas/ruleset.schema.json`

## tsqlrefine.json

`tsqlrefine.json` supports these top-level properties:

- `compatLevel` (integer): SQL Server compatibility level used by the parser (`100`, `110`, `120`, `150`, `160`).
- `preset` (string): name of a built-in preset ruleset (e.g. `"recommended"`, `"strict"`).
- `ruleset` (string): path to a custom ruleset file. Can be relative to the working directory or absolute. For built-in presets, use `preset` instead.
- `plugins` (array): plugin DLLs to load (optional).

> **Note**: If both `preset` and `ruleset` are specified, `preset` takes precedence. The `--preset` CLI option overrides both.

Example:

```json
{
  "compatLevel": 150,
  "preset": "recommended",
  "plugins": [
    { "path": "plugins/custom-rules.dll", "enabled": true }
  ],
  "formatting": {
    "keywordCasing": "upper",
    "identifierCasing": "preserve",
    "indentStyle": "spaces",
    "indentSize": 4
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
    "identifierCasing": "preserve",
    "commaStyle": "trailing",
    "maxLineLength": 0,
    "insertFinalNewline": true,
    "trimTrailingWhitespace": true
  }
}
```

**Available options:**

- `indentStyle` (string): `"spaces"` or `"tabs"`. Default: `"spaces"`
- `indentSize` (integer): Number of spaces per indent level (for spaces) or tab width (for tabs). Default: `4`
- `keywordCasing` (string): Keyword casing style
  - `"preserve"`: Keep original casing
  - `"upper"`: UPPERCASE (default)
  - `"lower"`: lowercase
  - `"pascal"`: PascalCase
- `identifierCasing` (string): Identifier casing style
  - `"preserve"`: Keep original casing (default)
  - `"upper"`: UPPERCASE
  - `"lower"`: lowercase
  - `"pascal"`: PascalCase
  - `"camel"`: camelCase
- `commaStyle` (string): Comma placement
  - `"trailing"`: `SELECT a, b, c` (default)
  - `"leading"`: `SELECT a ,b ,c`
- `maxLineLength` (integer): Maximum line length (0 = no limit). Default: `0`
- `insertFinalNewline` (boolean): Insert final newline at end of file. Default: `true`
- `trimTrailingWhitespace` (boolean): Trim trailing whitespace on lines. Default: `true`

**Priority order:**

1. CLI arguments (`--indent-style`, `--indent-size`)
2. `.editorconfig` settings (for indentation only)
3. `tsqlrefine.json` formatting section
4. Built-in defaults

## Ruleset file

A ruleset file declares a list of rules and whether they are enabled.

Example `custom-ruleset.json`:

```json
{
  "rules": [
    { "id": "avoid-null-comparison", "enabled": true },
    { "id": "order-by-in-subquery", "enabled": false }
  ]
}
```

## Preset rulesets

tsqlrefine includes built-in preset rulesets:

| Preset | Rules | Description |
|--------|-------|-------------|
| `recommended` | 58 | Balanced production use with semantic analysis (default) |
| `strict` | 97 | Maximum enforcement including all style/cosmetic rules |
| `strict-logic` | 74 | Comprehensive correctness and semantic analysis without cosmetic style rules |
| `pragmatic` | 34 | Production-ready minimum focusing on safety and critical issues |
| `security-only` | 13 | Security vulnerabilities and critical safety only |

Use the `preset` property in `tsqlrefine.json` or the `--preset` CLI option:

```json
{
  "preset": "recommended"
}
```

```bash
tsqlrefine lint --preset strict file.sql
```

**Choosing a preset:**
- Start with `recommended` for most projects - it provides balanced production-ready linting
- Use `pragmatic` for minimal enforcement in legacy codebases or when first introducing linting
- Use `strict-logic` for comprehensive correctness checking without style/cosmetic noise
- Use `strict` for maximum enforcement when you want both logic and style consistency
- Use `security-only` for security-focused code review or CI gates

