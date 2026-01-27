# Duplicate Go

**Rule ID:** `duplicate-go`
**Category:** Style
**Severity:** Information
**Fixable:** No

## Description

Avoid consecutive GO batch separators.

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT 1;\nGO\nGO\nSELECT 2;
```

### Good

```sql
SELECT 1;\nGO\nSELECT 2;
```

## Configuration

To disable this rule, add it to your `tsqlrefine.json`:

```json
{
  "ruleset": "custom-ruleset.json"
}
```

In `custom-ruleset.json`:

```json
{
  "rules": [
    { "id": "duplicate-go", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
