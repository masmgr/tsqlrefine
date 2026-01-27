# Object Property

**Rule ID:** `object-property`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Prohibit OBJECTPROPERTY function; use OBJECTPROPERTYEX or sys catalog views instead

## Rationale

This rule identifies patterns that can cause performance issues. Following this rule helps ensure your queries run efficiently and scale well with data growth.

## Examples

### Bad

```sql
SELECT OBJECTPROPERTY(OBJECT_ID('dbo.Users'), 'TableHasPrimaryKey')
```

### Good

```sql
SELECT OBJECTPROPERTYEX(OBJECT_ID('dbo.Users'), 'BaseType')
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
    { "id": "object-property", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
