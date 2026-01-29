# Granular SQL Casing Demo

## Input SQL
```sql
declare @userid int = 1;
select u.userid, count(*) as total, getdate() from dbo.users u where u.isactive = 1;
```

## Default (No Granular Casing)
Uses legacy KeywordCasing=Upper, IdentifierCasing=Preserve:
```sql
DECLARE @userid int = 1;
SELECT u.userid, COUNT(*) AS total, GETDATE() FROM dbo.users u WHERE u.isactive = 1;
```

## Granular Casing (Recommended Defaults)
- Keywords: UPPER
- Functions: UPPER
- Types: lower
- Schema: lower
- Tables: UPPER
- Columns: UPPER
- Variables: lower

Result:
```sql
DECLARE @userid int = 1;
SELECT u.USERID, COUNT(*) AS TOTAL, GETDATE() FROM dbo.USERS u WHERE u.ISACTIVE = 1;
```

## Custom: All Lowercase
- Keywords: lower
- Functions: lower
- Types: lower
- Schema: lower
- Tables: lower
- Columns: lower
- Variables: lower

Result:
```sql
declare @userid int = 1;
select u.userid, count(*) as total, getdate() from dbo.users u where u.isactive = 1;
```

## Custom: Mixed Style
- Keywords: UPPER
- Functions: lower
- Types: UPPER
- Schema: UPPER
- Tables: lower
- Columns: PascalCase (Note: Use none to preserve)
- Variables: UPPER

Result would be customizable based on settings.
