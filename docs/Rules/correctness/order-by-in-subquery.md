# Order By In Subquery

**Rule ID:** `order-by-in-subquery`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Disallows invalid ORDER BY in subqueries unless paired with TOP, OFFSET, FOR XML, or FOR JSON (SQL Server error Msg 1033).

## Rationale

This rule prevents code that may produce incorrect results or runtime errors. Following this rule helps ensure your SQL code behaves as expected and produces reliable results.

## Examples

### Bad

```sql
-- Example showing rule violation
```

### Good

```sql
SELECT id, name FROM users ORDER BY name;
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
    { "id": "order-by-in-subquery", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
