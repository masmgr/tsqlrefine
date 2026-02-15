# Require THROW or RAISERROR in CATCH

**Rule ID:** `require-throw-or-raiserror-in-catch`
**Category:** Transactions
**Severity:** Information
**Fixable:** No

## Description

Detects CATCH blocks that do not propagate the error via THROW, RAISERROR, or RETURN with error code.

## Rationale

A CATCH block that logs the error but does not propagate it creates a "silent failure" — the caller has no way to know an error occurred. This can lead to:

- **Data inconsistency**: The caller proceeds as if the operation succeeded
- **Hidden bugs**: Errors go unnoticed in production
- **Difficult debugging**: The root cause is buried in a log table

Acceptable error propagation includes:
- `THROW` (recommended) — rethrows the original error
- `RAISERROR(...)` — raises a new error with appropriate severity
- `RETURN <value>` — returns an error code (e.g., `RETURN -1`)

A bare `RETURN;` (without value) is not considered error propagation.

## Examples

### Bad

```sql
-- Logs error but does not propagate
BEGIN TRY
    INSERT INTO dbo.Orders (Id) VALUES (1);
END TRY
BEGIN CATCH
    INSERT INTO dbo.ErrorLog (Message) VALUES (ERROR_MESSAGE());
END CATCH;

-- Bare RETURN does not propagate
BEGIN TRY
    SELECT 1;
END TRY
BEGIN CATCH
    RETURN;
END CATCH;
```

### Good

```sql
-- Log and rethrow
BEGIN TRY
    INSERT INTO dbo.Orders (Id) VALUES (1);
END TRY
BEGIN CATCH
    INSERT INTO dbo.ErrorLog (Message) VALUES (ERROR_MESSAGE());
    THROW;
END CATCH;

-- RAISERROR
BEGIN TRY
    SELECT 1;
END TRY
BEGIN CATCH
    DECLARE @msg NVARCHAR(4000) = ERROR_MESSAGE();
    RAISERROR(@msg, 16, 1);
END CATCH;

-- RETURN with error code
BEGIN TRY
    SELECT 1;
END TRY
BEGIN CATCH
    INSERT INTO dbo.ErrorLog (Message) VALUES (ERROR_MESSAGE());
    RETURN -1;
END CATCH;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "require-throw-or-raiserror-in-catch", "enabled": false }
  ]
}
```

## See Also

- [avoid-catch-swallowing](avoid-catch-swallowing.md) - Detects empty CATCH blocks that suppress errors
- [require-try-catch-for-transaction](require-try-catch-for-transaction.md) - Requires TRY/CATCH for transactions
- [TsqlRefine Rules Documentation](../README.md)
