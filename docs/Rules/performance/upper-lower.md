# Upper Lower

**Rule ID:** `upper-lower`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Detects UPPER or LOWER functions applied to columns in WHERE, JOIN ON, or HAVING predicates which prevents index usage

## Rationale

This rule identifies patterns that can cause performance issues. Following this rule helps ensure your queries run efficiently and scale well with data growth.

## Examples

### Bad

```sql
SELECT * FROM users WHERE UPPER(username) = 'ADMIN';
```

### Good

```sql
SELECT * FROM users WHERE username = UPPER('admin');
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
    { "id": "upper-lower", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
