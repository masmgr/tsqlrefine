# Require Try Catch For Transaction

**Rule ID:** `require-try-catch-for-transaction`
**Category:** Transaction Safety
**Severity:** Warning
**Fixable:** No

## Description

Requires TRY/CATCH around explicit transactions to ensure errors trigger rollback and cleanup consistently.

## Rationale

Explicit transactions **without TRY/CATCH error handling** leave the database in an **inconsistent state** when errors occur:

1. **Open transactions remain active**: If an error occurs after `BEGIN TRANSACTION` but before `COMMIT`, the transaction stays open
   - Locks are held indefinitely
   - Other queries are blocked
   - Transaction log cannot be truncated

2. **Partial data changes**: Some statements may succeed while others fail, violating atomicity
   - Database is in an inconsistent state
   - Business logic invariants are broken

3. **No automatic rollback**: Unlike some databases, SQL Server does **not** automatically rollback transactions on all errors
   - Some errors (e.g., constraint violations) continue execution
   - Transaction remains open waiting for explicit COMMIT or ROLLBACK

4. **Difficult debugging**: Production errors may leave orphaned transactions that are hard to diagnose

**TRY/CATCH ensures:**
- **Automatic rollback** on any error in the transaction
- **Consistent error handling** across all error types
- **Clean transaction state** (committed or rolled back, never left open)
- **Error propagation** (THROW re-raises error after cleanup)

## Examples

### Bad

```sql
-- No error handling (transaction may be left open)
BEGIN TRANSACTION;
UPDATE users SET active = 1;
COMMIT;  -- What if UPDATE fails?

-- Multiple statements without TRY/CATCH
BEGIN TRANSACTION;
INSERT INTO orders (customer_id, total) VALUES (1, 100);
INSERT INTO order_items (order_id, product_id) VALUES (SCOPE_IDENTITY(), 5);
UPDATE inventory SET quantity = quantity - 1 WHERE product_id = 5;
COMMIT;  -- If any INSERT/UPDATE fails, transaction is left open

-- Nested transactions without error handling (dangerous)
BEGIN TRANSACTION;
    UPDATE accounts SET balance = balance - 100 WHERE account_id = 1;
    BEGIN TRANSACTION;  -- Nested
        UPDATE accounts SET balance = balance + 100 WHERE account_id = 2;
    COMMIT;
COMMIT;  -- If inner fails, outer transaction is corrupted
```

### Good

```sql
-- Basic TRY/CATCH with transaction
BEGIN TRY
    BEGIN TRANSACTION;
    UPDATE users SET active = 1;
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK;
    THROW;  -- Re-raise error after cleanup
END CATCH;

-- Multiple statements with proper error handling
BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO orders (customer_id, total) VALUES (1, 100);
    DECLARE @OrderId INT = SCOPE_IDENTITY();

    INSERT INTO order_items (order_id, product_id) VALUES (@OrderId, 5);

    UPDATE inventory SET quantity = quantity - 1 WHERE product_id = 5;

    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK;

    -- Log error details
    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();

    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;

-- Save point for partial rollback
BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE accounts SET balance = balance - 100 WHERE account_id = 1;

    SAVE TRANSACTION SavePoint1;

    BEGIN TRY
        UPDATE accounts SET balance = balance + 100 WHERE account_id = 2;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION SavePoint1;  -- Partial rollback
        THROW;
    END CATCH;

    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK;
    THROW;
END CATCH;

-- Stored procedure with transaction and error handling
CREATE PROCEDURE dbo.ProcessOrder
    @CustomerId INT,
    @ProductId INT,
    @Quantity INT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Validate inventory
        DECLARE @Available INT;
        SELECT @Available = quantity FROM inventory WHERE product_id = @ProductId;

        IF @Available < @Quantity
            THROW 50001, 'Insufficient inventory', 1;

        -- Create order
        INSERT INTO orders (customer_id, total)
        VALUES (@CustomerId, @Quantity * 10.00);

        DECLARE @OrderId INT = SCOPE_IDENTITY();

        -- Add order items
        INSERT INTO order_items (order_id, product_id, quantity)
        VALUES (@OrderId, @ProductId, @Quantity);

        -- Update inventory
        UPDATE inventory
        SET quantity = quantity - @Quantity
        WHERE product_id = @ProductId;

        COMMIT;

        RETURN @OrderId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK;

        -- Return error to caller
        THROW;
    END CATCH;
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
    { "id": "require-try-catch-for-transaction", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
