# Set Concat Null Yields Null

**Rule ID:** `set-concat-null-yields-null`
**Category:** Transactions
**Severity:** Warning
**Fixable:** No

## Description

Files should start with SET CONCAT_NULL_YIELDS_NULL ON within the first 10 statements.

## Rationale

Setting `CONCAT_NULL_YIELDS_NULL ON` at the beginning of scripts is **required for ANSI-compliant NULL concatenation** and is a **best practice** for SQL Server development:

1. **Required for indexed views and computed columns**:
   - SQL Server **requires** `CONCAT_NULL_YIELDS_NULL ON` when creating or using indexes on views or computed columns

2. **NULL concatenation behavior**:
   - `CONCAT_NULL_YIELDS_NULL ON` (recommended): `'text' + NULL` returns `NULL` (ANSI standard)
   - `CONCAT_NULL_YIELDS_NULL OFF`: `'text' + NULL` returns `'text'` (non-standard behavior)
   - ANSI standard requires that concatenation with NULL produces NULL

3. **Future compatibility**:
   - Microsoft **deprecated** `CONCAT_NULL_YIELDS_NULL OFF`
   - Future versions may **remove** support for the OFF setting
   - New code should always use `CONCAT_NULL_YIELDS_NULL ON`

4. **Environment consistency**:
   - Different connection drivers and clients may have different default settings
   - Explicitly setting this option ensures consistent behavior across all environments
   - OLE DB and ODBC default to ON, but legacy applications may override this

5. **SET settings baked into objects**:
   - When creating stored procedures, functions, views, or triggers, the current SET options are **saved with the object**
   - Must be set **before** CREATE PROCEDURE/FUNCTION/VIEW

## Examples

### Bad

```sql
-- No CONCAT_NULL_YIELDS_NULL setting (uses session default, may be OFF)
CREATE PROCEDURE dbo.BuildFullName AS
BEGIN
    SELECT FirstName + ' ' + LastName FROM dbo.Users;
END;
GO

-- CONCAT_NULL_YIELDS_NULL set to OFF
SET CONCAT_NULL_YIELDS_NULL OFF;
GO
CREATE PROCEDURE dbo.BuildFullName AS
BEGIN
    -- When LastName is NULL, this returns 'John ' instead of NULL
    SELECT FirstName + ' ' + LastName FROM dbo.Users;
END;
GO
```

### Good

```sql
-- Set CONCAT_NULL_YIELDS_NULL ON at the beginning of script
SET CONCAT_NULL_YIELDS_NULL ON;
GO

CREATE PROCEDURE dbo.BuildFullName AS
BEGIN
    -- When LastName is NULL, returns NULL (ANSI standard)
    -- Use COALESCE or CONCAT() for NULL-safe concatenation
    SELECT CONCAT(FirstName, ' ', LastName) FROM dbo.Users;
END;
GO

-- Complete script with all recommended settings
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET CONCAT_NULL_YIELDS_NULL ON;
GO

CREATE FUNCTION dbo.GetDisplayName(@FirstName NVARCHAR(50), @LastName NVARCHAR(50))
RETURNS NVARCHAR(101)
AS
BEGIN
    RETURN CONCAT(@FirstName, ' ', @LastName);
END;
GO
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
    { "id": "set-concat-null-yields-null", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
