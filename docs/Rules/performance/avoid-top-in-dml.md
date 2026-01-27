# Avoid Top In Dml

**Rule ID:** `avoid-top-in-dml`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Disallows TOP in UPDATE/DELETE; it is frequently non-deterministic and easy to misuse without a carefully designed ordering strategy.

## Rationale

This rule identifies patterns that can cause performance issues. Following this rule helps ensure your queries run efficiently and scale well with data growth.

## Examples

### Bad

```sql
UPDATE TOP (10) users SET name = 'John';
```

### Good

```sql
UPDATE users SET name = 'John' WHERE id = 1;
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
    { "id": "avoid-top-in-dml", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
