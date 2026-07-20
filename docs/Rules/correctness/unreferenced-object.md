# Unreferenced Object

**Rule ID:** `unreferenced-object`  
**Category:** Correctness  
**Severity:** Information  
**Fixable:** No

Detects procedures, functions, and views with no incoming references in the object catalog.
Self-references do not make an object referenced. Application entry points can be excluded with a
comma-separated `entrypoints` option containing unqualified or schema-qualified object names.

```sql
-- Reported when no other collected object or script calls this procedure.
CREATE PROCEDURE dbo.RunNightlyImport
AS
SELECT 1;
```

```json
{
  "rules": {
    "unreferenced-object": {
      "options": { "entrypoints": "dbo.RunNightlyImport,dbo.PublicApi" }
    }
  }
}
```
