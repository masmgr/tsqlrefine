# Semantic Return After Statements

**Rule ID:** `semantic/return-after-statements`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects unreachable statements after a RETURN statement in stored procedures or functions.

## Rationale

This rule prevents code that may produce incorrect results or runtime errors. Following this rule helps ensure your SQL code behaves as expected and produces reliable results.

## Examples

### Bad

```sql
BEGIN RETURN; SELECT 1; END
```

### Good

```sql
BEGIN SELECT 1; RETURN; END
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
    { "id": "semantic-return-after-statements", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
