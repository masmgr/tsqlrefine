# Require Semicolon Before THROW

**Rule ID:** `require-semicolon-before-throw`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Requires the statement immediately before `THROW` to end with a semicolon.

## Rationale

`THROW` requires the preceding statement to be terminated. Omitting the semicolon can change how SQL Server parses the batch instead of producing the intended exception. The most dangerous example occurs after `ROLLBACK TRANSACTION`: `THROW` can be interpreted as the transaction or savepoint name, silently disabling error propagation.

## Examples

### Bad

```sql
BEGIN CATCH
    ROLLBACK TRANSACTION
    THROW;
END CATCH;
```

### Good

```sql
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH;
```

A standalone `THROW` or one used directly as the body of an `IF` statement does not have a preceding statement and is not reported.

## Configuration

```json
{
  "rules": [
    { "id": "require-semicolon-before-throw", "enabled": false }
  ]
}
```

## See Also

- [Semicolon Termination](../style/semicolon-termination.md)
- [Require THROW or RAISERROR in CATCH](../transactions/require-throw-or-raiserror-in-catch.md)
