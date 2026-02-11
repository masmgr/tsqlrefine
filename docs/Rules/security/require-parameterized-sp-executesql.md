# Require Parameterized sp_executesql

**Rule ID:** `require-parameterized-sp-executesql`
**Category:** Security
**Severity:** Warning
**Fixable:** No

## Description

Detects sp_executesql calls without proper parameterization or with string concatenation.

## Rationale

`sp_executesql` is the recommended way to execute dynamic SQL in SQL Server because it supports parameterization, which prevents SQL injection. However, using `sp_executesql` without parameter definitions negates this benefit:

- **Without parameters**: `EXEC sp_executesql N'SELECT * FROM T WHERE Id = 1'` — the value is embedded in the SQL string, no different from `EXEC()`
- **With concatenation**: Building the SQL string with `+` operator defeats parameterization

Always provide parameter definitions (second argument) and parameter values to get the security and performance benefits of parameterized queries.

## Examples

### Bad

```sql
-- No parameter definitions
EXEC sp_executesql N'SELECT * FROM dbo.Users WHERE Id = 1';

-- Variable without parameter definitions
EXEC sp_executesql @sql;
```

### Good

```sql
-- Properly parameterized
EXEC sp_executesql
    N'SELECT * FROM dbo.Users WHERE Id = @id',
    N'@id INT',
    @id = 1;

-- Multiple parameters
EXEC sp_executesql
    N'SELECT * FROM dbo.Users WHERE Id = @id AND Name = @name',
    N'@id INT, @name NVARCHAR(100)',
    @id = 1,
    @name = N'test';
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "require-parameterized-sp-executesql", "enabled": false }
  ]
}
```

## See Also

- [avoid-exec-dynamic-sql](avoid-exec-dynamic-sql.md) - Detects EXEC with dynamic SQL
- [TsqlRefine Rules Documentation](../README.md)
