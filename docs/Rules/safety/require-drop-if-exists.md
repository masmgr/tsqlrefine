# Require DROP IF EXISTS

**Rule ID:** `require-drop-if-exists`
**Category:** Safety
**Severity:** Information
**Fixable:** No

## Description

Requires IF EXISTS on DROP statements for idempotent deployment scripts.

## Rationale

A bare `DROP TABLE/VIEW/PROCEDURE/FUNCTION` statement will fail with an error if the target object does not exist. This makes deployment scripts fragile and non-idempotent - running the same script twice will fail on the second execution.

Using `DROP ... IF EXISTS` (available since SQL Server 2016) makes scripts safe to re-run and is considered a best practice for deployment and migration scripts.

This rule skips temporary tables (`#temp`) since they are transient and `IF EXISTS` is less critical for them.

## Examples

### Bad

```sql
-- Will fail if object doesn't exist
DROP TABLE dbo.Users;
DROP PROCEDURE dbo.MyProc;
DROP VIEW dbo.MyView;
DROP FUNCTION dbo.MyFunc;
```

### Good

```sql
-- Idempotent - safe to re-run
DROP TABLE IF EXISTS dbo.Users;
DROP PROCEDURE IF EXISTS dbo.MyProc;
DROP VIEW IF EXISTS dbo.MyView;
DROP FUNCTION IF EXISTS dbo.MyFunc;

-- Temp tables are excluded from this rule
DROP TABLE #TempTable;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "require-drop-if-exists", "enabled": false }
  ]
}
```

## See Also

- [dangerous-ddl](dangerous-ddl.md) - Detects destructive DDL operations
- [TsqlRefine Rules Documentation](../README.md)
