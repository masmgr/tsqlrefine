# variable-used-before-assignment

## Summary

Reports a local variable read before it is definitely assigned on every path reaching that use.

## Examples

```sql
DECLARE @value int;
IF @load = 1
    SET @value = 1;
SELECT @value; -- @value is unassigned on the false path.
```

Procedure and function parameters and declarations with initializers start assigned. Assignment
through `SET`, `SELECT`, `FETCH ... INTO`, and output EXEC arguments is tracked. The analysis uses
intersection at control-flow joins and skips scopes containing `GOTO`.

## Category

Correctness

## Severity

Warning

## Fixable

No
