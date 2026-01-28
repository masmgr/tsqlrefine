# Transaction Without Commit Or Rollback

**Rule ID:** `transaction-without-commit-or-rollback`
**Category:** Transactions
**Severity:** Error
**Fixable:** No

## Description

Detects `BEGIN TRANSACTION` statements without corresponding `COMMIT` or `ROLLBACK` in the same batch.

## Rationale

Orphaned transactions are a common cause of production issues:

- **Locks held indefinitely** - Blocks other queries, causes timeouts
- **Transaction log growth** - Cannot truncate log while transaction is open
- **Lock escalation** - Row locks escalate to table locks, blocking entire table
- **Cascading failures** - One orphaned transaction can bring down entire application
- **Hard to diagnose** - Requires checking `@@TRANCOUNT` and `sp_who2` to find culprit

## Examples

### Bad

```sql
-- Missing commit entirely
CREATE PROCEDURE ProcessOrder @OrderId INT
AS
BEGIN
    BEGIN TRANSACTION;

    UPDATE Orders SET Status = 'Processing' WHERE Id = @OrderId;
    UPDATE Inventory SET Quantity = Quantity - 1 WHERE ProductId = ...;

    -- Missing COMMIT or ROLLBACK!
END;

-- Missing rollback on error path
CREATE PROCEDURE UpdateUser @UserId INT, @Status VARCHAR(50)
AS
BEGIN
    BEGIN TRANSACTION;

    UPDATE Users SET Status = @Status WHERE Id = @UserId;

    IF @Status = 'Active'
    BEGIN
        COMMIT TRANSACTION;
    END
    -- Missing ROLLBACK for inactive status!
END;
```

### Good

```sql
-- Explicit commit
CREATE PROCEDURE ProcessOrder @OrderId INT
AS
BEGIN
    BEGIN TRANSACTION;

    UPDATE Orders SET Status = 'Processing' WHERE Id = @OrderId;
    UPDATE Inventory SET Quantity = Quantity - 1 WHERE ProductId = ...;

    COMMIT TRANSACTION;
END;

-- Error handling with rollback
CREATE PROCEDURE UpdateUser @UserId INT, @Status VARCHAR(50)
AS
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE Users SET Status = @Status WHERE Id = @UserId;
        INSERT INTO AuditLog (UserId, Action) VALUES (@UserId, 'StatusChange');

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;

-- Conditional logic with all paths covered
CREATE PROCEDURE ConditionalUpdate @Id INT, @Type VARCHAR(50)
AS
BEGIN
    BEGIN TRANSACTION;

    IF @Type = 'A'
    BEGIN
        UPDATE TableA SET Status = 'Done' WHERE Id = @Id;
        COMMIT TRANSACTION;
    END
    ELSE
    BEGIN
        UPDATE TableB SET Status = 'Done' WHERE Id = @Id;
        COMMIT TRANSACTION;  -- Both paths have COMMIT
    END
END;
```

## Common Patterns

### Pattern 1: Proper Error Handling

```sql
CREATE PROCEDURE SafeTransaction
AS
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;

        -- Multiple operations
        UPDATE Table1 SET Col = 'Value';
        INSERT INTO Table2 VALUES ('Data');
        DELETE FROM Table3 WHERE Id = 1;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        -- Log error
        INSERT INTO ErrorLog (Message, ErrorTime)
        VALUES (ERROR_MESSAGE(), GETDATE());

        THROW;
    END CATCH;
END;
```

### Pattern 2: Nested Transactions (Save Points)

```sql
CREATE PROCEDURE NestedOperation @Level INT
AS
BEGIN
    BEGIN TRANSACTION;

    UPDATE MainTable SET Value = @Level;

    -- Savepoint for partial rollback
    SAVE TRANSACTION SavePoint1;

    BEGIN TRY
        UPDATE DetailTable SET Status = 'Processed';
    END TRY
    BEGIN CATCH
        -- Rollback to savepoint, not entire transaction
        ROLLBACK TRANSACTION SavePoint1;
    END CATCH;

    COMMIT TRANSACTION;  -- Must commit outer transaction
END;
```

### Pattern 3: Transaction with Validation

```sql
CREATE PROCEDURE ValidatedTransaction @Amount DECIMAL(18,2)
AS
BEGIN
    BEGIN TRANSACTION;

    UPDATE Account SET Balance = Balance - @Amount WHERE Id = @SourceId;

    -- Validation
    IF (SELECT Balance FROM Account WHERE Id = @SourceId) < 0
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50000, 'Insufficient funds', 1;
    END

    UPDATE Account SET Balance = Balance + @Amount WHERE Id = @TargetId;

    COMMIT TRANSACTION;
END;
```

## Anti-Patterns

### Anti-Pattern 1: Relying on Implicit Rollback

```sql
-- BAD: Expecting connection close to rollback
CREATE PROCEDURE ImplicitRollback
AS
BEGIN
    BEGIN TRANSACTION;
    UPDATE Users SET Status = 'Active';
    -- Connection closes -> rolls back
    -- But locks held until connection timeout!
END;
```

**Why bad:** Locks held until connection timeout/pool reclaim.

### Anti-Pattern 2: Missing Rollback in Conditional

```sql
-- BAD: Not all code paths have COMMIT/ROLLBACK
CREATE PROCEDURE ConditionalMissing @Type INT
AS
BEGIN
    BEGIN TRANSACTION;

    IF @Type = 1
    BEGIN
        UPDATE Table1 SET Col = 'A';
        COMMIT TRANSACTION;
    END
    -- Missing ELSE with COMMIT/ROLLBACK!
END;
```

### Anti-Pattern 3: Transaction Across Batches

```sql
-- BAD: Cannot track across GO
BEGIN TRANSACTION;
UPDATE Table1 SET Col = 'A';
GO  -- Batch boundary

COMMIT TRANSACTION;  -- Different batch!
```

**Why bad:** Rule cannot detect COMMIT in different batch.

## Configuration

To disable this rule:

```json
{
  "ruleset": "custom-ruleset.json"
}
```

In `custom-ruleset.json`:

```json
{
  "rules": [
    { "id": "transaction-without-commit-or-rollback", "enabled": false }
  ]
}
```

## Limitations

This rule has significant limitations due to static analysis constraints:

1. **GO batch boundaries** - Cannot track transactions across `GO` separators
2. **Dynamic SQL** - Cannot analyze `EXEC('BEGIN TRAN ...')`
3. **Stored procedure calls** - Cannot track if called proc commits/rollbacks
4. **Complex control flow** - May produce false positives with nested IF/WHILE
5. **@@TRANCOUNT** logic - Cannot analyze runtime transaction count checks

**Recommendation:** Use this rule to catch obvious errors. For complex scenarios, use runtime monitoring:
```sql
-- Add at end of stored procedures
IF @@TRANCOUNT > 0
    RAISERROR('Orphaned transaction detected!', 16, 1);
```

## Best Practices

1. **Always use TRY/CATCH** with transactions
2. **Check `@@TRANCOUNT > 0`** before ROLLBACK in CATCH
3. **Keep transactions short** - Reduces lock contention
4. **Avoid cross-batch transactions** - Use stored procedures instead
5. **Set `XACT_ABORT ON`** - Auto-rollback on errors (see [require-xact-abort-on](require-xact-abort-on.md))

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [require-try-catch-for-transaction](require-try-catch-for-transaction.md) - Requires TRY/CATCH for transaction safety
- [require-xact-abort-on](require-xact-abort-on.md) - Auto-rollback on errors
- [catch-swallowing](catch-swallowing.md) - Related error handling rule
- [Microsoft Documentation: Transactions](https://docs.microsoft.com/en-us/sql/t-sql/language-elements/transactions-transact-sql)
