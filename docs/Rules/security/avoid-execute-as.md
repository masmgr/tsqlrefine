# Avoid Execute As

**Rule ID:** `avoid-execute-as`
**Category:** Security
**Severity:** Information
**Fixable:** No

## Description

Detects EXECUTE AS usage for privilege escalation. EXECUTE AS can change the security context and may lead to unintended privilege escalation.

## Rationale

The `EXECUTE AS` statement and clause allow code to run under a different security context than the caller. While sometimes necessary, this is a common vector for privilege escalation attacks. `EXECUTE AS OWNER`, `EXECUTE AS SELF`, or impersonating a specific user/login can grant elevated permissions that the original caller should not have. This rule flags all non-CALLER usages so they can be reviewed for security implications.

`EXECUTE AS CALLER` is excluded because it is the default behavior and does not change the security context.

## Examples

### Bad

```sql
-- Standalone EXECUTE AS
EXECUTE AS USER = 'dbo';
SELECT * FROM sys.databases;
REVERT;

-- EXECUTE AS in stored procedure
CREATE PROCEDURE dbo.MyProc
WITH EXECUTE AS OWNER
AS
BEGIN
    SELECT 1;
END;

-- EXECUTE AS in function
CREATE FUNCTION dbo.MyFunc()
RETURNS INT
WITH EXECUTE AS SELF
AS
BEGIN
    RETURN 1;
END;

-- EXECUTE AS in trigger
CREATE TRIGGER dbo.MyTrigger ON dbo.MyTable
WITH EXECUTE AS OWNER
AFTER INSERT
AS
BEGIN
    SELECT 1;
END;
```

### Good

```sql
-- No EXECUTE AS
CREATE PROCEDURE dbo.SafeProc
AS
BEGIN
    SELECT 1;
END;

-- EXECUTE AS CALLER (default, no privilege change)
CREATE PROCEDURE dbo.CallerProc
WITH EXECUTE AS CALLER
AS
BEGIN
    SELECT 1;
END;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "avoid-execute-as", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
