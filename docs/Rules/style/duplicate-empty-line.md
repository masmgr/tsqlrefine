# Duplicate Empty Line

**Rule ID:** `duplicate-empty-line`
**Category:** Style
**Severity:** Information
**Fixable:** No

## Description

Avoid consecutive empty lines (more than one blank line in a row).

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT 1;\n\n\nSELECT 2;
```

### Good

```sql
SELECT 1;\n\nSELECT 2;
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
    { "id": "duplicate-empty-line", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
