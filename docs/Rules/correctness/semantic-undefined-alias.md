# Semantic Undefined Alias

**Rule ID:** `semantic/undefined-alias`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Detects references to undefined table aliases in column qualifiers.

## Rationale

This rule prevents code that may produce incorrect results or runtime errors. Following this rule helps ensure your SQL code behaves as expected and produces reliable results.

## Examples

### Bad

```sql
SELECT u.id FROM users WHERE x.active = 1;
```

### Good

```sql
SELECT u.id FROM users u WHERE u.active = 1;
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
    { "id": "semantic/undefined-alias", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
