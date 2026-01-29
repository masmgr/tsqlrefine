# Require Xact Abort On

**Rule ID:** `require-xact-abort-on`
**Category:** Transaction Safety
**Severity:** Warning
**Fixable:** No

## Description

Requires SET XACT_ABORT ON with explicit transactions to ensure runtime errors reliably abort and roll back work, preventing partial commits.

## Rationale

SET XACT_ABORT ON ensures runtime errors automatically roll back entire transactions, preventing dangerous partial commits.

**Without XACT_ABORT ON** (dangerous default behavior):

- Some errors continue transaction execution (partial commit risk)
- Explicit error checking needed after every statement
- Easy to miss errors and commit partial work
- Transaction may be left open after error

**With XACT_ABORT ON** (safe behavior):

- Any runtime error aborts and rolls back transaction automatically
- Connection also terminates on error (clean failure)
- Guaranteed all-or-nothing behavior
- No need for explicit error checking after each statement

**Example of danger without XACT_ABORT ON**:

```sql
-- XACT_ABORT OFF (default) - DANGEROUS
BEGIN TRANSACTION;
    INSERT INTO Orders (Id, Total) VALUES (1, 100);  -- Succeeds
    INSERT INTO Orders (Id, Total) VALUES (1, 200);  -- Error: Duplicate key
    -- Transaction STILL OPEN! No automatic rollback
COMMIT;  -- Commits first insert (partial commit!)
```

**Common errors that benefit from XACT_ABORT ON**:

- Constraint violations (PRIMARY KEY, FOREIGN KEY, CHECK, UNIQUE)
- Data type conversion errors
- Arithmetic overflow
- Deadlock victim
- Timeout errors

**Best practice**: Always use SET XACT_ABORT ON with explicit transactions in stored procedures to ensure transactional integrity.

## Examples

### Bad

```sql
-- Missing SET XACT_ABORT ON (dangerous)
BEGIN TRANSACTION;
    UPDATE Users SET Active = 1 WHERE UserId = 123;
    UPDATE Orders SET Processed = 1 WHERE UserId = 123;
    -- If second UPDATE fails, first UPDATE already committed!
COMMIT;

-- Another bad example: partial commit risk
CREATE PROCEDURE uspTransferFunds
    @FromAccount INT,
    @ToAccount INT,
    @Amount DECIMAL(18,2)
AS
BEGIN
    BEGIN TRANSACTION;
        -- No XACT_ABORT ON
        UPDATE Accounts SET Balance = Balance - @Amount WHERE AccountId = @FromAccount;
        -- If error occurs here, first update may commit
        UPDATE Accounts SET Balance = Balance + @Amount WHERE AccountId = @ToAccount;
    COMMIT;
END;
```

### Good

```sql
-- SET XACT_ABORT ON ensures automatic rollback on error
SET XACT_ABORT ON;

BEGIN TRANSACTION;
    UPDATE Users SET Active = 1 WHERE UserId = 123;
    UPDATE Orders SET Processed = 1 WHERE UserId = 123;
    -- Any error automatically rolls back entire transaction
COMMIT;

-- Complete example with XACT_ABORT ON
CREATE PROCEDURE uspTransferFunds
    @FromAccount INT,
    @ToAccount INT,
    @Amount DECIMAL(18,2)
AS
BEGIN
    SET XACT_ABORT ON;  -- Must be before BEGIN TRANSACTION

    BEGIN TRANSACTION;
        UPDATE Accounts SET Balance = Balance - @Amount WHERE AccountId = @FromAccount;
        UPDATE Accounts SET Balance = Balance + @Amount WHERE AccountId = @ToAccount;
        -- Any error automatically rolls back both updates
    COMMIT;
END;

-- With TRY-CATCH for error handling
CREATE PROCEDURE uspProcessOrder
    @OrderId INT
AS
BEGIN
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;
            UPDATE Orders SET Status = 'Processing' WHERE OrderId = @OrderId;
            INSERT INTO OrderLog (OrderId, Message) VALUES (@OrderId, 'Processing started');
            UPDATE Inventory SET Quantity = Quantity - 1 WHERE ProductId = (SELECT ProductId FROM Orders WHERE OrderId = @OrderId);
        COMMIT;
    END TRY
    BEGIN CATCH
        -- XACT_ABORT already rolled back transaction
        -- Log error and re-throw
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrorMessage, 16, 1);
    END CATCH
END;
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
    { "id": "require-xact-abort-on", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
