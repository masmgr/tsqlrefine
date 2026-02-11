# Avoid SET ROWCOUNT

**Rule ID:** `avoid-set-rowcount`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects SET ROWCOUNT statements which are deprecated and can cause unexpected behavior with triggers and nested statements.

## Rationale

`SET ROWCOUNT n` limits the number of rows affected by subsequent statements. However, this feature is deprecated in SQL Server and has several problems:

- **Affects triggers**: SET ROWCOUNT applies to triggers fired by the statement, potentially causing data integrity issues
- **Nested statement interference**: The row limit propagates into nested stored procedures and functions
- **Deprecated**: Microsoft recommends using `TOP` instead, and SET ROWCOUNT will not work with INSERT, UPDATE, MERGE, and DELETE in future versions

`SET ROWCOUNT 0` (which disables the limit) is allowed by this rule.

## Examples

### Bad

```sql
-- SET ROWCOUNT with positive integer
SET ROWCOUNT 100;
SELECT * FROM dbo.Users;

-- SET ROWCOUNT with variable
DECLARE @rows INT = 50;
SET ROWCOUNT @rows;
DELETE FROM dbo.OldRecords WHERE Status = 'Archived';
```

### Good

```sql
-- SET ROWCOUNT 0 resets/disables (allowed)
SET ROWCOUNT 0;

-- Use TOP instead
SELECT TOP 100 * FROM dbo.Users;

-- Use TOP in DML
DELETE TOP (1000) FROM dbo.OldRecords WHERE Status = 'Archived';
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "avoid-set-rowcount", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
