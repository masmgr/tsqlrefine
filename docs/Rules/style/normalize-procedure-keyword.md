# Normalize Procedure Keyword

**Rule ID:** `normalize-procedure-keyword`
**Category:** Style
**Severity:** Information
**Fixable:** Yes

## Description

Normalizes 'PROC' to 'PROCEDURE' for consistency.

## Rationale

T-SQL allows both `PROC` and `PROCEDURE` in DDL statements. While both are functionally equivalent, using the full `PROCEDURE` keyword consistently improves code readability and clarity:

1. **Consistency**: Using the full keyword form throughout a codebase makes the code more uniform
2. **Clarity**: `PROCEDURE` is more explicit and self-documenting than the abbreviated `PROC`
3. **Searchability**: Consistent keyword usage makes it easier to search and grep through code
4. **Standards alignment**: Many coding standards prefer full keyword forms for clarity
5. **Documentation**: Full keywords make DDL statements more readable in schema documentation

## Examples

### Bad

```sql
-- Abbreviated PROC in CREATE
CREATE PROC dbo.GetUsers
AS
BEGIN
    SELECT * FROM dbo.Users;
END;

-- Abbreviated PROC in ALTER
ALTER PROC dbo.GetUsers
AS
BEGIN
    SELECT UserId, UserName FROM dbo.Users;
END;

-- Abbreviated PROC in DROP
DROP PROC dbo.OldProcedure;

-- Multiple procedures with inconsistent style
CREATE PROC dbo.Proc1 AS SELECT 1;
CREATE PROCEDURE dbo.Proc2 AS SELECT 2;  -- Inconsistent
CREATE PROC dbo.Proc3 AS SELECT 3;
```

### Good

```sql
-- Full PROCEDURE in CREATE
CREATE PROCEDURE dbo.GetUsers
AS
BEGIN
    SELECT * FROM dbo.Users;
END;

-- Full PROCEDURE in ALTER
ALTER PROCEDURE dbo.GetUsers
AS
BEGIN
    SELECT UserId, UserName FROM dbo.Users;
END;

-- Full PROCEDURE in DROP
DROP PROCEDURE dbo.OldProcedure;

-- Consistent PROCEDURE keyword usage
CREATE PROCEDURE dbo.Proc1 AS SELECT 1;
CREATE PROCEDURE dbo.Proc2 AS SELECT 2;
CREATE PROCEDURE dbo.Proc3 AS SELECT 3;

-- With schema binding and other options
CREATE PROCEDURE dbo.GetActiveUsers
    @Status NVARCHAR(50)
WITH EXECUTE AS OWNER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UserId, UserName
    FROM dbo.Users
    WHERE Status = @Status;
END;
```

## Auto-Fix

This rule supports auto-fixing. The `PROC` keyword will be replaced with `PROCEDURE`.

To apply the fix:

```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- fix file.sql
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
    { "id": "normalize-procedure-keyword", "enabled": false }
  ]
}
```

## See Also

- [normalize-execute-keyword](normalize-execute-keyword.md) - Normalizes EXEC to EXECUTE
- [normalize-transaction-keyword](normalize-transaction-keyword.md) - Normalizes TRAN to TRANSACTION
- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
