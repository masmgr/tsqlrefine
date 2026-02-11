# Set Ansi Padding

**Rule ID:** `set-ansi-padding`
**Category:** Transactions
**Severity:** Warning
**Fixable:** No

## Description

Files should start with SET ANSI_PADDING ON within the first 10 statements.

## Rationale

Setting `ANSI_PADDING ON` at the beginning of scripts is **required for consistent storage behavior** and is a **best practice** for SQL Server development:

1. **Required for indexed views and computed columns**:
   - SQL Server **requires** `ANSI_PADDING ON` when creating or using indexes on views or computed columns
   - Filtered indexes also require this setting

2. **Trailing space behavior for CHAR/VARCHAR**:
   - `ANSI_PADDING ON` (recommended): `CHAR(n)` columns pad with spaces to the defined width; `VARCHAR(n)` preserves trailing spaces as inserted
   - `ANSI_PADDING OFF`: `CHAR(n)` behaves like `VARCHAR`; `VARCHAR(n)` trims trailing spaces

3. **Binary column padding**:
   - `ANSI_PADDING ON`: `BINARY(n)` pads with trailing zeros; `VARBINARY(n)` preserves trailing zeros
   - `ANSI_PADDING OFF`: Trailing zeros are trimmed

4. **Permanent column-level setting**:
   - Unlike most SET options, `ANSI_PADDING` is stored **per-column at creation time**
   - Changing the session setting later does **not** affect existing columns
   - This makes it critical to set correctly **before** creating tables

5. **SET settings baked into objects**:
   - When creating stored procedures, functions, views, or triggers, the current SET options are **saved with the object**
   - Must be set **before** CREATE PROCEDURE/FUNCTION/VIEW

## Examples

### Bad

```sql
-- No ANSI_PADDING setting (uses session default, may be OFF)
CREATE PROCEDURE dbo.InsertUser AS
BEGIN
    INSERT INTO dbo.Users (Name) VALUES ('John');
END;
GO

-- ANSI_PADDING set to OFF
SET ANSI_PADDING OFF;
GO
CREATE PROCEDURE dbo.InsertUser AS
BEGIN
    INSERT INTO dbo.Users (Name) VALUES ('John');
END;
GO
```

### Good

```sql
-- Set ANSI_PADDING ON at the beginning of script
SET ANSI_PADDING ON;
GO

CREATE PROCEDURE dbo.InsertUser AS
BEGIN
    INSERT INTO dbo.Users (Name) VALUES ('John');
END;
GO

-- Complete script with all recommended settings
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_WARNINGS ON;
SET ANSI_PADDING ON;
GO

CREATE TABLE dbo.Products (
    ProductId INT NOT NULL,
    ProductCode CHAR(10) NOT NULL,  -- Properly padded with spaces
    ProductName VARCHAR(100) NOT NULL
);
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
    { "id": "set-ansi-padding", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
