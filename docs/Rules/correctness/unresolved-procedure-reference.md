# Unresolved Procedure Reference

**Rule ID:** `unresolved-procedure-reference`  
**Category:** Correctness  
**Severity:** Warning  
**Fixable:** No

Detects static procedure and user-defined function calls that are missing from an authoritative
object catalog. Dynamic calls, four-part names, ambiguous matches, and references outside the
catalog database scope are skipped.

```sql
-- Reported when dbo.MissingProcedure is absent from an authoritative objects.json.
EXEC dbo.MissingProcedure @id = 1;
```

Generate the catalog with `schema collect-objects` and supply it through `--objects-catalog` or
`schema.objectsCatalogPath`.
