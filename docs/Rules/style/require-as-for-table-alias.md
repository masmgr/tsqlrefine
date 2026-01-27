# Require As For Table Alias

**Rule ID:** `require-as-for-table-alias`
**Category:** Style
**Severity:** Information
**Fixable:** Yes

## Description

Table aliases should use the AS keyword

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT * FROM users u;
```

### Good

```sql
SELECT * FROM users AS u;
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
    { "id": "require-as-for-table-alias", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
