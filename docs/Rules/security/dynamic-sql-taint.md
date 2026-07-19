# Dynamic SQL Taint

**Rule ID:** `dynamic-sql-taint`  
**Category:** Security  
**Severity:** Error  
**Fixable:** No

## Description

Tracks values from procedure and function parameters, table columns, and unknown expressions
through variable assignments into `EXEC(...)` and `sp_executesql` SQL-text arguments. Reports
dynamic SQL when an unsafe value can reach an execution sink on any supported control-flow path.

Unlike syntax-only dynamic SQL rules, this rule follows assignments across variables and branches.
The existing `avoid-exec-dynamic-sql` and `require-parameterized-sp-executesql` rules remain useful
for inexpensive pattern checks, so more than one diagnostic can identify the same call.

## Examples

### Bad

```sql
CREATE PROCEDURE dbo.FindUser @userName nvarchar(100)
AS
BEGIN
    DECLARE @sql nvarchar(max) = N'SELECT * FROM dbo.Users WHERE Name = ''';
    SET @sql = @sql + @userName + N'''';
    EXEC sys.sp_executesql @sql;
END;
```

Copying the value through additional variables does not make it trusted:

```sql
DECLARE @fragment nvarchar(max) = @userInput;
DECLARE @sql nvarchar(max) = N'SELECT * FROM dbo.Users WHERE Name = '''
    + @fragment + N'''';
EXEC(@sql);
```

`QUOTENAME` and quote escaping are purpose-specific. Using either in the wrong SQL context is
reported:

```sql
-- QUOTENAME produces a delimited identifier, not a string-literal value.
SET @sql = N'SELECT * FROM dbo.Users WHERE Name = ''' + QUOTENAME(@name) + N'''';

-- Escaping apostrophes does not make a safe identifier.
SET @sql = N'SELECT * FROM ' + REPLACE(@tableName, N'''', N'''''');
```

### Good

Prefer `sp_executesql` parameters for values:

```sql
EXEC sys.sp_executesql
    N'SELECT * FROM dbo.Users WHERE Name = @name',
    N'@name nvarchar(100)',
    @name = @userName;
```

Use `QUOTENAME` only for identifiers that cannot be parameters:

```sql
SET @sql = N'SELECT * FROM ' + QUOTENAME(@tableName);
EXEC sys.sp_executesql @sql;
```

Escaped values are accepted only while the symbolic SQL text is inside a string literal:

```sql
SET @sql = N'SELECT * FROM dbo.Users WHERE Name = '''
    + REPLACE(@name, N'''', N'''''') + N'''';
EXEC sys.sp_executesql @sql;
```

An explicit conversion to a numeric SQL type is also accepted for numeric insertion:

```sql
SET @sql = N'SELECT * FROM dbo.Users WHERE Id = ' + CONVERT(int, @id);
EXEC sys.sp_executesql @sql;
```

## Analysis Boundaries

- Analysis is intraprocedural and uses the control-flow graph for each batch or routine.
- Symbolic SQL text is bounded to 32 segments. More complex values widen to `Unknown` and are
  reported at a sink.
- Scopes containing unsupported `GOTO` control flow are skipped to avoid misleading results.
- Table-column assignments are treated as untrusted. A typed option for changing this source
  policy is planned with per-rule options.
- `QUOTENAME` length and nullability do not make input trusted; callers should still account for a
  possible `NULL` result.

## Configuration

```json
{
  "rules": [
    { "id": "dynamic-sql-taint", "enabled": false }
  ]
}
```

## See Also

- [avoid-exec-dynamic-sql](avoid-exec-dynamic-sql.md)
- [require-parameterized-sp-executesql](require-parameterized-sp-executesql.md)
