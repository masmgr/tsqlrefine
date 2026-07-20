# unreachable-statement

## Summary

Reports statements that cannot be reached because of `RETURN`, `THROW`, loop control, or a
statically false branch.

## Examples

```sql
RETURN;
SELECT 1; -- Unreachable.
```

Literal equality and inequality predicates such as `IF 1 = 0` are folded. The rule follows the
shared CFG and skips scopes containing `GOTO` rather than guessing label targets.

## Category

Correctness

## Severity

Warning

## Fixable

No
