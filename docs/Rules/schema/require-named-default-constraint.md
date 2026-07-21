# Require Named Default Constraint

**Rule ID:** `require-named-default-constraint`
**Category:** Schema
**Severity:** Warning
**Fixable:** No

Requires every `DEFAULT` constraint on a permanent table column to have an explicit name. Temporary tables and table variables are excluded.

```sql
-- Bad
CREATE TABLE dbo.T_ITEM (IS_VALID tinyint DEFAULT ((1)));

-- Good
CREATE TABLE dbo.T_ITEM (
    IS_VALID tinyint CONSTRAINT DF_T_ITEM_IS_VALID DEFAULT ((1))
);
```
