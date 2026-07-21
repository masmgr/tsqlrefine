# exec-parameter-count-mismatch

## Summary

Reports a statically resolved `EXEC` call when required procedure parameters are missing or
the call contains extra positional arguments.

## Examples

```sql
-- dbo.SaveUser requires @id and @active; @name has a default.
EXEC dbo.SaveUser 42; -- @active is missing
```

```sql
EXEC dbo.SaveUser 42, @active = 1; -- valid
```

## Detection

The rule resolves the procedure through the object catalog, binds positional and named
arguments, accounts for parameter default values, and rejects `DEFAULT` for a parameter
that has no default. Dynamic, external, unresolved, and
ambiguous procedure calls are skipped. Generate and supply `objects.json` with
`schema collect-objects` and `--objects-catalog` (or `schema.objectsCatalogPath`).

## Category

Correctness

## Severity

Error

## Fixable

No
