# Require SAVE TRANSACTION in Nested Transactions

**Rule ID:** `require-save-transaction-in-nested`
**Category:** Transactions
**Severity:** Information
**Fixable:** No

## Description

Detects nested BEGIN TRANSACTION without SAVE TRANSACTION. Without a savepoint, ROLLBACK in a nested transaction rolls back the entire outer transaction.

## Rationale

In SQL Server, transactions do not truly nest. When you issue `BEGIN TRANSACTION` inside an existing transaction, the `@@TRANCOUNT` increments but a `ROLLBACK` will roll back all transactions — not just the inner one. This is a common source of unexpected behavior, especially in stored procedures called from within a transaction.

By using `SAVE TRANSACTION` with a named savepoint before the nested `BEGIN TRANSACTION`, you can `ROLLBACK TRANSACTION SavepointName` to undo only the work done after the savepoint, leaving the outer transaction intact. This rule flags nested `BEGIN TRANSACTION` when no `SAVE TRANSACTION` has been issued, helping prevent accidental full rollbacks.

## Examples

### Bad

```sql
-- Nested BEGIN TRANSACTION without SAVE TRANSACTION
BEGIN TRANSACTION;
    BEGIN TRANSACTION;  -- flagged: no savepoint
        SELECT 1;
    COMMIT;
COMMIT;

-- Triple nesting without savepoints
BEGIN TRANSACTION;
    BEGIN TRANSACTION;      -- flagged
        BEGIN TRANSACTION;  -- flagged
            SELECT 1;
        COMMIT;
    COMMIT;
COMMIT;
```

### Good

```sql
-- Nested transaction with SAVE TRANSACTION
BEGIN TRANSACTION;
    SAVE TRANSACTION SavePoint1;
    BEGIN TRANSACTION;
        SELECT 1;
    COMMIT;
COMMIT;

-- Single transaction (no nesting)
BEGIN TRANSACTION;
    SELECT 1;
COMMIT;

-- SAVE TRANSACTION without nesting
BEGIN TRANSACTION;
    SAVE TRANSACTION sp1;
    SELECT 1;
COMMIT;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "require-save-transaction-in-nested", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
