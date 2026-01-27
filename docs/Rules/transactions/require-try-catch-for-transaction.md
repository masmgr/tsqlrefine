# Require Try Catch For Transaction

**Rule ID:** `require-try-catch-for-transaction`
**Category:** Transaction Safety
**Severity:** Warning
**Fixable:** No

## Description

Requires TRY/CATCH around explicit transactions to ensure errors trigger rollback and cleanup consistently.

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
BEGIN TRANSACTION; UPDATE users SET active = 1; COMMIT;
```

### Good

```sql
BEGIN TRY
    BEGIN TRANSACTION;
    UPDATE users SET active = 1;
    COMMIT;
END TRY
BEGIN CATCH
    ROLLBACK;
    THROW;
END CATCH;
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
    { "id": "require-try-catch-for-transaction", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
