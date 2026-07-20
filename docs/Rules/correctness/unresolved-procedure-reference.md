# Unresolved Procedure Reference

**Rule ID:** `unresolved-procedure-reference`  
**Category:** Correctness  
**Severity:** Warning  
**Fixable:** No

Detects static procedure and user-defined function calls that are missing from an authoritative
object catalog. Dynamic calls, four-part names, ambiguous matches, and references outside the
catalog database scope are skipped. Unqualified or `sys`-qualified calls to system stored
procedures and extended procedures (a known set such as `sp_executesql`, `sp_getapplock`,
`xp_cmdshell`, plus any other unqualified `sp_`/`xp_`-prefixed name) are also skipped, since they
are never expected to appear in the object catalog.

```sql
-- Reported when dbo.MissingProcedure is absent from an authoritative objects.json.
EXEC dbo.MissingProcedure @id = 1;
```

Generate the catalog with `schema collect-objects` and supply it through `--objects-catalog` or
`schema.objectsCatalogPath`.
