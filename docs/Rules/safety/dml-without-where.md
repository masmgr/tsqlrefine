# Dml Without Where

**Rule ID:** `dml-without-where`
**Category:** Safety
**Severity:** Error
**Fixable:** No

## Description

Detects UPDATE/DELETE statements without WHERE clause to prevent unintended mass data modifications.

## Rationale

This rule prevents destructive or dangerous operations that could lead to data loss or corruption. Following this rule helps protect your database from accidental or unintended modifications.

## Examples

### Bad

```sql
UPDATE users SET active = 1;
```

### Good

```sql
UPDATE users SET active = 1 WHERE id = 5;
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
    { "id": "dml-without-where", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
