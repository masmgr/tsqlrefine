# Avoid Hardcoded Password

**Rule ID:** `avoid-hardcoded-password`
**Category:** Security
**Severity:** Warning
**Fixable:** No

## Description

Detects plaintext password literals in `CREATE LOGIN`, `ALTER LOGIN`, `OPENROWSET`, and `OPENDATASOURCE` connection strings.

## Rationale

Passwords committed in SQL scripts remain available in repository history, build logs, deployment artifacts, and backups. Rotate an exposed credential and supply secrets through a protected deployment mechanism instead.

Hashed login password values are not reported because they are not reusable plaintext credentials.

## Examples

### Bad

```sql
CREATE LOGIN app_user WITH PASSWORD = 'secret';

SELECT *
FROM OPENDATASOURCE(
    'MSOLEDBSQL',
    'Server=db;User ID=app;Password=secret'
).app.dbo.Users;
```

### Good

```sql
CREATE LOGIN domain_user FROM WINDOWS;

SELECT *
FROM OPENROWSET(
    'MSOLEDBSQL',
    'Server=db;Trusted_Connection=yes',
    'SELECT 1'
) AS source;
```

## Configuration

```json
{
  "rules": [
    { "id": "avoid-hardcoded-password", "enabled": false }
  ]
}
```

## See Also

- [Avoid EXEC Dynamic SQL](avoid-exec-dynamic-sql.md)
- [Avoid OPENROWSET and OPENDATASOURCE](avoid-openrowset-opendatasource.md)
