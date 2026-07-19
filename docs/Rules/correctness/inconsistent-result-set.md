# inconsistent-result-set

## Summary

Reports procedures that return different result-set column shapes on different execution paths.

## Examples

```sql
IF @full = 1
    SELECT Id, Name FROM dbo.Users;
ELSE
    SELECT Id FROM dbo.Users;
```

Column count and statically available aliases or column names are compared as an ordered result
shape. Sequential result sets that occur on every path are retained as a consistent sequence;
`SELECT` variable assignment and `SELECT ... INTO` are not treated as returned result sets.

## Category

Correctness

## Severity

Warning

## Fixable

No
