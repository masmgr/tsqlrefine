# Avoid Catch Swallowing

**Rule ID:** `avoid-catch-swallowing`
**Category:** Transactions
**Severity:** Warning
**Fixable:** No

## Description

Detects CATCH blocks that suppress errors without proper logging or rethrowing, creating silent failures.

## Rationale

Error suppression makes debugging impossible. When a CATCH block silently swallows errors:

- **Production incidents** become impossible to diagnose
- **Data corruption** may go unnoticed
- **Transaction state** becomes unpredictable
- **Monitoring/alerting** systems are bypassed
- **Users** don't know operations failed

Errors should either be:
1. **Logged** to a persistent store (error table, application log)
2. **Rethrown** to the caller using `THROW` or `RAISERROR`

## Examples

### Bad

```sql
-- Silent error suppression (worst case)
BEGIN TRY
    UPDATE Users SET Status = 'Active';
END TRY
BEGIN CATCH
    -- Nothing here - error completely swallowed!
END CATCH;

-- Only PRINT (non-persistent, rarely monitored)
BEGIN TRY
    DELETE FROM Orders WHERE OrderId = @Id;
END TRY
BEGIN CATCH
    PRINT 'Error occurred';  -- Nobody will see this
END CATCH;

-- Only SELECT (output not captured in stored procedures)
BEGIN TRY
    INSERT INTO Customers VALUES (@Name, @Email);
END TRY
BEGIN CATCH
    SELECT ERROR_MESSAGE();  -- Lost in stored procedure calls
END CATCH;
```

### Good

```sql
-- Log to persistent error table
BEGIN TRY
    UPDATE Users SET Status = 'Active';
END TRY
BEGIN CATCH
    INSERT INTO ErrorLog (Message, ErrorNumber, ErrorTime)
    VALUES (ERROR_MESSAGE(), ERROR_NUMBER(), GETDATE());

    THROW;  -- Re-throw to caller
END CATCH;

-- Minimal: Just re-throw
BEGIN TRY
    DELETE FROM Orders WHERE OrderId = @Id;
END TRY
BEGIN CATCH
    THROW;  -- Let caller handle error
END CATCH;

-- Contextual error handling with re-throw
BEGIN TRY
    INSERT INTO Customers (Name, Email)
    VALUES (@Name, @Email);
END TRY
BEGIN CATCH
    -- Log with context
    DECLARE @ErrorMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorNum INT = ERROR_NUMBER();

    INSERT INTO ErrorLog (Context, ErrorMessage, ErrorNumber)
    VALUES ('InsertCustomer', @ErrorMsg, @ErrorNum);

    -- Re-throw original error
    THROW;
END CATCH;
```

## Common Patterns

### Pattern 1: Conditional Error Handling

Sometimes you want to handle specific errors but re-throw others:

```sql
BEGIN TRY
    INSERT INTO Users (Username, Email)
    VALUES (@Username, @Email);
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 2627  -- Duplicate key
    BEGIN
        -- Handle duplicate gracefully
        SELECT @UserId = UserId
        FROM Users
        WHERE Username = @Username;
    END
    ELSE
    BEGIN
        -- Re-throw unexpected errors
        THROW;
    END
END CATCH;
```

### Pattern 2: Cleanup with Re-throw

```sql
BEGIN TRY
    -- Complex operation
    EXEC dbo.ProcessOrder @OrderId;
END TRY
BEGIN CATCH
    -- Cleanup temporary resources
    IF OBJECT_ID('tempdb..#OrderDetails') IS NOT NULL
        DROP TABLE #OrderDetails;

    -- Still must re-throw!
    THROW;
END CATCH;
```

### Pattern 3: Error Translation

```sql
BEGIN TRY
    EXEC dbo.ExternalSystemCall @Param;
END TRY
BEGIN CATCH
    -- Translate system error to business error
    DECLARE @BusinessError NVARCHAR(500) =
        'Failed to process order: ' + ERROR_MESSAGE();

    -- Log original error
    INSERT INTO ErrorLog (Message) VALUES (ERROR_MESSAGE());

    -- Throw business-friendly error
    THROW 50000, @BusinessError, 1;
END CATCH;
```

## Anti-Patterns

### Anti-Pattern 1: Empty CATCH

```sql
BEGIN TRY
    -- risky operation
END TRY
BEGIN CATCH
    -- Nothing - never do this!
END CATCH;
```

**Why bad:** Errors disappear without trace.

### Anti-Pattern 2: CATCH with only SET

```sql
DECLARE @Success BIT = 1;

BEGIN TRY
    UPDATE Users SET Status = 'Active';
END TRY
BEGIN CATCH
    SET @Success = 0;  -- Only sets flag
END CATCH;

IF @Success = 1
    PRINT 'Success';  -- Misleading success message
```

**Why bad:** No error information preserved, caller doesn't know what failed.

### Anti-Pattern 3: CATCH with only RAISERROR (old style)

If using `RAISERROR` (SQL Server 2008-2012), must propagate error correctly:

```sql
BEGIN TRY
    -- operation
END TRY
BEGIN CATCH
    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();

    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    -- Use THROW instead in SQL Server 2012+
END CATCH;
```

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
    { "id": "avoid-catch-swallowing", "enabled": false }
  ]
}
```

## Limitations

- **Cannot detect** logging to application logs (outside SQL)
- **Cannot detect** external error handling frameworks
- **Nested TRY/CATCH** may produce complex scenarios

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [require-try-catch-for-transaction](require-try-catch-for-transaction.md) - Related rule for transaction error handling
- [Microsoft Documentation: TRY...CATCH](https://docs.microsoft.com/en-us/sql/t-sql/language-elements/try-catch-transact-sql)
- [Microsoft Documentation: THROW](https://docs.microsoft.com/en-us/sql/t-sql/language-elements/throw-transact-sql)
