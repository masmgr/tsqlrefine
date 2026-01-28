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
  ]
}
```

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

