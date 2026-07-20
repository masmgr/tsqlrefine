# exec-output-not-captured

## Summary

Reports calls to output parameters that omit the `OUTPUT` keyword and therefore discard the
returned value.

## Examples

```sql
DECLARE @found bit;
EXEC dbo.TryGet 42, @found;        -- returned value is discarded
EXEC dbo.TryGet 42, @found OUTPUT; -- valid
```

The rule checks only statically resolved calls backed by an object catalog. It does not report
input parameters or unresolved, dynamic, or external procedure calls.

## Category

Correctness

## Severity

Warning

## Fixable

No
