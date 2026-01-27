# Semantic Duplicate Alias

**Rule ID:** `semantic/duplicate-alias`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Detects duplicate table aliases in the same scope, which causes ambiguous references.

## Rationale

This rule prevents code that may produce incorrect results or runtime errors. Following this rule helps ensure your SQL code behaves as expected and produces reliable results.

## Examples

### Bad

```sql
SELECT * FROM users u JOIN orders u ON u.id = u.user_id;
```

### Good

```sql
SELECT * FROM users u JOIN orders o ON u.id = o.user_id;
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
    { "id": "semantic/duplicate-alias", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
