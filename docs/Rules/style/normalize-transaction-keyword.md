# Normalize Transaction Keyword

**Rule ID:** `normalize-transaction-keyword`
**Category:** Style
**Severity:** Information
**Fixable:** Yes

## Description

Normalizes 'TRAN' to 'TRANSACTION' and requires explicit 'TRANSACTION' after COMMIT/ROLLBACK.

## Rationale

T-SQL provides multiple forms for transaction-related statements. This rule enforces consistent, explicit usage:

1. **TRAN vs TRANSACTION**: `TRAN` is an abbreviation of `TRANSACTION`. Using the full form improves readability
2. **Standalone COMMIT/ROLLBACK**: While `COMMIT` and `ROLLBACK` work alone, `COMMIT TRANSACTION` and `ROLLBACK TRANSACTION` are more explicit about what is being committed or rolled back
3. **Consistency**: Uniform transaction syntax across a codebase reduces cognitive load
4. **Clarity**: Explicit `TRANSACTION` keyword makes the intent clear, especially in complex scripts

### What This Rule Detects

- `TRAN` keyword (in BEGIN TRAN, COMMIT TRAN, ROLLBACK TRAN, SAVE TRAN)
- Standalone `COMMIT` without `TRANSACTION`
- Standalone `ROLLBACK` without `TRANSACTION`

### What This Rule Does NOT Flag

- `COMMIT TRANSACTION` / `ROLLBACK TRANSACTION` (correct form)
- `COMMIT WORK` / `ROLLBACK WORK` (ANSI-compatible syntax)
- `COMMIT` or `ROLLBACK` followed by a savepoint name

## Examples

### Bad

```sql
-- Abbreviated TRAN keyword
BEGIN TRAN;
    UPDATE dbo.Users SET Status = 'Active' WHERE UserId = 1;
COMMIT TRAN;

-- Standalone COMMIT/ROLLBACK (implicit TRANSACTION)
BEGIN TRANSACTION;
    INSERT INTO dbo.Orders (CustomerId, Total) VALUES (1, 100.00);
COMMIT;  -- Missing explicit TRANSACTION

-- Mixed styles
BEGIN TRAN;
    DELETE FROM dbo.TempData;
ROLLBACK;  -- Inconsistent and implicit

-- In error handling
BEGIN TRY
    BEGIN TRAN;
    -- ... operations ...
    COMMIT TRAN;
END TRY
BEGIN CATCH
    ROLLBACK;  -- Standalone ROLLBACK
    THROW;
END CATCH;
```

### Good

```sql
-- Full TRANSACTION keyword
BEGIN TRANSACTION;
    UPDATE dbo.Users SET Status = 'Active' WHERE UserId = 1;
COMMIT TRANSACTION;

-- Explicit COMMIT TRANSACTION
BEGIN TRANSACTION;
    INSERT INTO dbo.Orders (CustomerId, Total) VALUES (1, 100.00);
COMMIT TRANSACTION;

-- Consistent explicit style
BEGIN TRANSACTION;
    DELETE FROM dbo.TempData;
ROLLBACK TRANSACTION;

-- In error handling (explicit)
BEGIN TRY
    BEGIN TRANSACTION;
    -- ... operations ...
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- With named transactions
BEGIN TRANSACTION OrderProcessing;
    -- ... operations ...
COMMIT TRANSACTION OrderProcessing;

-- With savepoints (explicit TRANSACTION)
BEGIN TRANSACTION;
    SAVE TRANSACTION BeforeUpdate;
    UPDATE dbo.Products SET Price = Price * 1.1;
    -- If something goes wrong:
    ROLLBACK TRANSACTION BeforeUpdate;
COMMIT TRANSACTION;

-- ANSI-compatible WORK syntax (not flagged)
BEGIN TRANSACTION;
    -- ... operations ...
COMMIT WORK;  -- ANSI syntax, acceptable
```

## Auto-Fix

This rule supports auto-fixing with the following transformations:

| Before | After |
|--------|-------|
| `TRAN` | `TRANSACTION` |
| `COMMIT;` | `COMMIT TRANSACTION;` |
| `ROLLBACK;` | `ROLLBACK TRANSACTION;` |

To apply the fix:

```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- fix file.sql
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
    { "id": "normalize-transaction-keyword", "enabled": false }
  ]
}
```

## See Also

- [normalize-execute-keyword](normalize-execute-keyword.md) - Normalizes EXEC to EXECUTE
- [normalize-procedure-keyword](normalize-procedure-keyword.md) - Normalizes PROC to PROCEDURE
- [transaction-without-commit-or-rollback](../transactions/transaction-without-commit-or-rollback.md) - Detects missing COMMIT/ROLLBACK
- [require-try-catch-for-transaction](../transactions/require-try-catch-for-transaction.md) - Requires TRY/CATCH for transactions
- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
