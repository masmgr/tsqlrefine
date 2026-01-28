# Semantic Set Variable

**Rule ID:** `semantic/set-variable`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Recommends using SELECT for variable assignment instead of SET for better performance and consistency.

## Rationale

Using a consistent variable-assignment pattern improves readability, and SELECT-based assignment can be preferable when assignments come from queries (including multi-variable assignment patterns). This rule encourages that consistency.

## Examples

### Bad

```sql
DECLARE @Count INT; SET @Count = 10;
```

### Good

```sql
DECLARE @Count INT; SELECT @Count = COUNT(*) FROM Users;
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
    { "id": "semantic/set-variable", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
