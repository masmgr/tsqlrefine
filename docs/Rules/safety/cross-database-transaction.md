# Cross Database Transaction

**Rule ID:** `cross-database-transaction`
**Category:** Safety
**Severity:** Warning
**Fixable:** No

## Description

Discourage cross-database transactions to avoid distributed transaction issues

## Rationale

This rule prevents destructive or dangerous operations that could lead to data loss or corruption. Following this rule helps protect your database from accidental or unintended modifications.

## Examples

### Bad

```sql
SELECT * FROM DB1.dbo.Table1
```

### Good

```sql
SELECT * FROM DB1.dbo.Table1
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
    { "id": "cross-database-transaction", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
