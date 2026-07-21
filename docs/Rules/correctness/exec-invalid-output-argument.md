# exec-invalid-output-argument

## Summary

Reports a statically resolved `EXEC` call that specifies `OUTPUT` for an input-only
procedure parameter.

## Examples

```sql
CREATE PROCEDURE dbo.FindUser @id int AS SELECT @id;
GO

DECLARE @id int = 1;
EXEC dbo.FindUser @id OUTPUT;
```

## Detection

The rule resolves the procedure through the object catalog and compares each argument
marked `OUTPUT` with the procedure signature. Dynamic, external, unresolved, and ambiguous
procedure calls are skipped.

## Category

Correctness

## Severity

Error

## Fixable

No
