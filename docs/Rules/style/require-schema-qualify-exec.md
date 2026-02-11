# Require Schema Qualify Exec

**Rule ID:** `require-schema-qualify-exec`
**Category:** Style
**Severity:** Warning
**Fixable:** No

## Description

Requires schema qualification on EXEC procedure calls (e.g., EXEC dbo.ProcName instead of EXEC ProcName).

## Rationale

Omitting the schema prefix on stored procedure calls introduces several problems:

1. **Name resolution overhead**: SQL Server must search multiple schemas to resolve unqualified names, which can cause unnecessary overhead
2. **Plan cache pollution**: Unqualified procedure names may generate separate plan cache entries per user's default schema, wasting memory
3. **Permission ambiguity**: Without explicit schema, the resolved procedure depends on the caller's default schema, leading to subtle permission issues
4. **Correctness risk**: If multiple schemas contain a procedure with the same name, the wrong one may be executed

### Exceptions

The following are automatically excluded from this rule:

- **Known system stored procedures**: `sp_executesql`, `sp_xml_preparedocument`, `sp_xml_removedocument`, `sp_prepare`, `sp_execute`, `sp_unprepare`, `sp_describe_first_result_set`, `sp_describe_undeclared_parameters`, `sp_getapplock`, `sp_releaseapplock`, `sp_addmessage`, `sp_dropmessage`, `xp_cmdshell`, `xp_sendmail`
- **Temporary procedures**: Names starting with `#` or `##`
- **Variable-based execution**: `EXEC @procVariable`

## Examples

### Bad

```sql
-- No schema qualification
EXEC GetUserById @UserId = 1;

-- User-defined sp_ procedure without schema
EXEC sp_MyCustomProc;

-- EXECUTE keyword without schema
EXECUTE ProcessOrders;
```

### Good

```sql
-- Schema qualified
EXEC dbo.GetUserById @UserId = 1;

-- System stored procedure (allowed without schema)
EXEC sp_executesql @sql;

-- Temp procedure (allowed)
EXEC #TempProc;

-- Variable-based execution (allowed)
DECLARE @proc NVARCHAR(100) = N'dbo.MyProc';
EXEC @proc;

-- Database and schema qualified
EXEC MyDatabase.dbo.MyProc;
```

## Configuration

To disable this rule, add it to your `tsqlrefine.json`:

```json
{
  "ruleset": "custom-ruleset.json"
}
```

In `custom-ruleset.json`:

```json
{
  "rules": [
    { "id": "require-schema-qualify-exec", "enabled": false }
  ]
}
```

## See Also

- [semantic-schema-qualify](semantic-schema-qualify.md) - Requires schema qualification on table references
- [normalize-execute-keyword](normalize-execute-keyword.md) - Normalizes EXEC to EXECUTE
- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
