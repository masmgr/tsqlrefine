# unused-variable

## Summary

Reports local variables and routine parameters that are declared but never read.

## Examples

```sql
DECLARE @value int;
SET @value = 1; -- Written, but never read.
```

Assignments do not count as reads. Compound assignments do count because they consume the prior
value. Analysis is performed independently for each batch, procedure, or function scope.

## Category

Correctness

## Severity

Information

## Fixable

No
