# Utc Datetime

**Rule ID:** `utc-datetime`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Detects local datetime functions (GETDATE, SYSDATETIME, CURRENT_TIMESTAMP, SYSDATETIMEOFFSET) and suggests UTC alternatives for consistency across time zones

## Rationale

This rule identifies patterns that can cause performance issues. Following this rule helps ensure your queries run efficiently and scale well with data growth.

## Examples

### Bad

```sql
SELECT GETDATE();
```

### Good

```sql
SELECT GETUTCDATE();
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
    { "id": "utc-datetime", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
