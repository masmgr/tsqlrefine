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
- `ruleset` (string): path to a ruleset file. Can be relative to the config file or absolute.
- `plugins` (array): plugin DLLs to load (optional).

Example:

```json
{
  "compatLevel": 150,
  "ruleset": "rulesets/recommended.json",
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

The repository includes preset rulesets under `rulesets/`:

- `rulesets/recommended.json`
- `rulesets/strict.json`
- `rulesets/security-only.json`

You can reference them from `tsqlrefine.json` via `ruleset`, or use the CLI `--preset` option (if supported by your version of the CLI).

