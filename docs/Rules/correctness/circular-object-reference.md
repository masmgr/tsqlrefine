# Circular Object Reference

**Rule ID:** `circular-object-reference`  
**Category:** Correctness  
**Severity:** Warning  
**Fixable:** No

Detects dependency cycles between collected procedures, functions, and views. Each definition in a
cycle receives a diagnostic in its defining file.

```sql
CREATE VIEW dbo.FirstView AS
SELECT Id FROM dbo.SecondView;
GO
CREATE VIEW dbo.SecondView AS
SELECT Id FROM dbo.FirstView;
```

Cycles make deployment ordering and schema evolution difficult. Extract shared logic or remove one
side of the dependency.
