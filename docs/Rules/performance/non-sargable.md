# Non Sargable

**Rule ID:** `non-sargable`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Detects functions applied to columns in WHERE, JOIN ON, or HAVING predicates which prevents index usage (excluding UPPER/LOWER and CAST/CONVERT)

## Rationale

This rule identifies patterns that can cause performance issues. Following this rule helps ensure your queries run efficiently and scale well with data growth.

## Examples

### Bad

```sql
SELECT * FROM users WHERE LTRIM(username) = 'admin';
```

### Good

```sql
SELECT * FROM users WHERE UPPER(username) = 'ADMIN';
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
    { "id": "non-sargable", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
