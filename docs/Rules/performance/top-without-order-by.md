# Top Without Order By

**Rule ID:** `top-without-order-by`
**Category:** Performance
**Severity:** Error
**Fixable:** No

## Description

Detects TOP clause without ORDER BY, which produces non-deterministic results.

## Rationale

This rule identifies patterns that can cause performance issues. Following this rule helps ensure your queries run efficiently and scale well with data growth.

## Examples

### Bad

```sql
SELECT TOP 10 * FROM users;
```

### Good

```sql
SELECT TOP 10 * FROM users ORDER BY id;
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
    { "id": "top-without-order-by", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
