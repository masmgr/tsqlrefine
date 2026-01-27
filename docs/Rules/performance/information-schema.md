# Information Schema

**Rule ID:** `information-schema`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Prohibit INFORMATION_SCHEMA views; use sys catalog views for better performance

## Rationale

This rule identifies patterns that can cause performance issues. Following this rule helps ensure your queries run efficiently and scale well with data growth.

## Examples

### Bad

```sql
SELECT * FROM INFORMATION_SCHEMA.TABLES
```

### Good

```sql
SELECT * FROM sys.tables
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
    { "id": "information-schema", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
