# Require As For Column Alias

**Rule ID:** `require-as-for-column-alias`
**Category:** Style
**Severity:** Information
**Fixable:** Yes

## Description

Column aliases should use the AS keyword

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT id userId FROM users;
```

### Good

```sql
SELECT id AS userId FROM users;
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
    { "id": "require-as-for-column-alias", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
