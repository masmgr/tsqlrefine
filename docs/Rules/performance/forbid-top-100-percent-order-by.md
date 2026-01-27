# Forbid Top 100 Percent Order By

**Rule ID:** `forbid-top-100-percent-order-by`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Forbids TOP 100 PERCENT ORDER BY; it is redundant and often ignored by the optimizer.

## Rationale

This rule identifies patterns that can cause performance issues. Following this rule helps ensure your queries run efficiently and scale well with data growth.

## Examples

### Bad

```sql
SELECT TOP 100 PERCENT * FROM users ORDER BY id;
```

### Good

```sql
SELECT TOP 100 PERCENT * FROM users;
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
    { "id": "forbid-top-100-percent-order-by", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
