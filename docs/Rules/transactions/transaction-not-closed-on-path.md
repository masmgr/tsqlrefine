# transaction-not-closed-on-path

## Summary

Reports a transaction opened by the current batch or routine when at least one reachable exit
path does not commit or roll it back.

## Examples

```sql
BEGIN TRANSACTION;
IF @valid = 1
    COMMIT TRANSACTION;
-- The false path leaves the transaction open.
```

The CFG analysis follows `IF/ELSE`, loops, `RETURN`, and `TRY/CATCH` paths. Nested transaction
depth, full versus savepoint rollback, `XACT_STATE()`, `@@TRANCOUNT`, and unknown procedure or
dynamic-SQL side effects are treated conservatively. Transactions owned by the caller are not
reported. Scopes containing `GOTO` are skipped.

## Relationship to Similar Rules

- `uncommitted-transaction` performs lightweight file-level matching.
- `avoid-transaction-without-commit` matches transaction depth within each batch.
- This rule provides the strongest path-sensitive check and reports transactions left open on a reachable exit.

## Category

Transactions

## Severity

Error

## Fixable

No
