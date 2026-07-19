# cursor-not-deallocated-on-path

## Summary

Reports an opened cursor when a reachable exit path does not execute `DEALLOCATE`.

## Examples

```sql
DECLARE items CURSOR FOR SELECT Id FROM dbo.Items;
OPEN items;
CLOSE items;
-- DEALLOCATE items is missing.
```

The analysis follows branches, loops, returns, and conservative `TRY/CATCH` exception edges.
`CLOSE` alone does not release the cursor allocation. Scopes containing `GOTO` are skipped.

## Category

Correctness

## Severity

Warning

## Fixable

No
