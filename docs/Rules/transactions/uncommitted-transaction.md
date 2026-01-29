# Uncommitted Transaction

**Rule ID:** `uncommitted-transaction`
**Category:** Transactions
**Severity:** Warning
**Fixable:** No

## Description

Detects `BEGIN TRANSACTION` statements without corresponding `COMMIT TRANSACTION` or `ROLLBACK TRANSACTION` in the same file.

## Rationale

Uncommitted transactions can cause serious production issues:

- **Resource locks** - Holds locks on tables and rows, blocking other queries
- **Transaction log growth** - Log cannot be truncated while transaction is open
- **Connection timeout issues** - Long-running transactions may timeout
- **Database blocking** - Can cascade and block other operations
- **Difficult troubleshooting** - Hard to identify the source of open transactions

This rule uses a greedy matching algorithm to pair each `BEGIN TRANSACTION` with the first available `COMMIT` or `ROLLBACK` statement that appears after it in the file.

## Examples

### Bad

```sql
-- Missing commit entirely
BEGIN TRANSACTION;
UPDATE Users SET LastLogin = GETDATE() WHERE Id = 1;
-- Missing COMMIT or ROLLBACK!

-- Multiple BEGIN with insufficient COMMIT
BEGIN TRANSACTION;
UPDATE Orders SET Status = 'Processed' WHERE OrderId = 100;

BEGIN TRANSACTION;
UPDATE OrderItems SET Shipped = 1 WHERE OrderId = 100;
COMMIT TRANSACTION; -- Only commits the second transaction
```

### Good

```sql
-- Explicit commit
BEGIN TRANSACTION;
UPDATE Users SET LastLogin = GETDATE() WHERE Id = 1;
COMMIT TRANSACTION;

-- Explicit rollback
BEGIN TRANSACTION;
DELETE FROM TempData WHERE CreatedDate < DATEADD(DAY, -7, GETDATE());
ROLLBACK TRANSACTION;

-- Transaction with TRY/CATCH
BEGIN TRY
    BEGIN TRANSACTION;
    UPDATE Accounts SET Balance = Balance - 100 WHERE AccountId = 1;
    UPDATE Accounts SET Balance = Balance + 100 WHERE AccountId = 2;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH

-- Nested transactions with commits
BEGIN TRANSACTION;
    UPDATE ParentTable SET UpdatedDate = GETDATE() WHERE Id = 1;

    BEGIN TRANSACTION;
        UPDATE ChildTable SET ProcessedDate = GETDATE() WHERE ParentId = 1;
    COMMIT TRANSACTION;

COMMIT TRANSACTION;
```

## Matching Algorithm

This rule uses a **greedy matching algorithm**:

1. Collects all `BEGIN TRANSACTION`, `COMMIT TRANSACTION`, and `ROLLBACK TRANSACTION` statements
2. For each `BEGIN TRANSACTION` (in order):
   - Finds the first unused `COMMIT` or `ROLLBACK` that appears after it
   - Marks that `COMMIT`/`ROLLBACK` as used
   - If no match is found, reports a diagnostic

This approach works well for most common patterns but has limitations (see below).

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
    { "id": "uncommitted-transaction", "enabled": false }
  ]
}
```

## Limitations

This rule has limitations due to static analysis constraints:

1. **Simple matching** - Uses greedy first-available matching, not true control flow analysis
2. **No control flow awareness** - Cannot detect conditional paths (IF/ELSE, WHILE, etc.)
3. **No stored procedure tracking** - Cannot follow `EXEC` calls to other procedures
4. **Dynamic SQL** - Cannot analyze `EXEC('BEGIN TRAN ...')`
5. **Cross-file transactions** - Only analyzes within a single file

**Example of false negative:**

```sql
BEGIN TRANSACTION;
UPDATE Table1 SET Col = 'A';

IF @condition = 1
    COMMIT TRANSACTION;
-- ELSE branch has no COMMIT/ROLLBACK (rule won't detect this!)
```

**Recommendation:** Use this rule as a first-pass check for obvious errors. For complete coverage, also use:
- [transaction-without-commit-or-rollback](transaction-without-commit-or-rollback.md) - More sophisticated batch-level analysis
- Runtime monitoring with `@@TRANCOUNT` checks

## Best Practices

1. **Always close transactions** - Every `BEGIN` should have corresponding `COMMIT` or `ROLLBACK`
2. **Use TRY/CATCH** - Wrap transactions in error handling (see [require-try-catch-for-transaction](require-try-catch-for-transaction.md))
3. **Keep transactions short** - Minimize time between BEGIN and COMMIT/ROLLBACK
4. **Avoid nesting** - Use savepoints instead of nested transactions when possible
5. **Set XACT_ABORT ON** - Auto-rollback on errors (see [require-xact-abort-on](require-xact-abort-on.md))

## Comparison with Similar Rules

- **uncommitted-transaction** (this rule) - File-level greedy matching, Warning severity
- **transaction-without-commit-or-rollback** - Batch-level analysis with control flow awareness, Error severity

Use both rules together for comprehensive transaction checking.

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [transaction-without-commit-or-rollback](transaction-without-commit-or-rollback.md) - Related batch-level transaction rule
- [require-try-catch-for-transaction](require-try-catch-for-transaction.md) - Requires TRY/CATCH for transaction safety
- [require-xact-abort-on](require-xact-abort-on.md) - Auto-rollback on errors
- [catch-swallowing](catch-swallowing.md) - Related error handling rule
- [Microsoft Documentation: Transactions](https://docs.microsoft.com/en-us/sql/t-sql/language-elements/transactions-transact-sql)
