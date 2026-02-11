# Set Ansi Warnings

**Rule ID:** `set-ansi-warnings`
**Category:** Transactions
**Severity:** Warning
**Fixable:** No

## Description

Files should start with SET ANSI_WARNINGS ON within the first 10 statements.

## Rationale

Setting `ANSI_WARNINGS ON` at the beginning of scripts is **required for correct aggregate and overflow behavior** and is a **best practice** for SQL Server development:

1. **Required for indexed views and computed columns**:
   - SQL Server **requires** `ANSI_WARNINGS ON` when creating or using indexes on views or computed columns
   - Without it, you get errors or incorrect behavior

2. **Aggregate NULL handling**:
   - `ANSI_WARNINGS ON` (recommended): Generates a warning when NULL values appear in aggregate functions (SUM, AVG, etc.)
   - `ANSI_WARNINGS OFF`: Silently ignores NULLs in aggregates without warning

3. **Arithmetic overflow behavior**:
   - `ANSI_WARNINGS ON`: Division by zero and arithmetic overflow produce errors
   - `ANSI_WARNINGS OFF`: Division by zero returns NULL, overflow may be silently truncated

4. **String truncation behavior**:
   - `ANSI_WARNINGS ON`: Inserting a string longer than the column width raises an error
   - `ANSI_WARNINGS OFF`: Silently truncates the string

5. **SET settings baked into objects**:
   - When creating stored procedures, functions, views, or triggers, the current SET options are **saved with the object**
   - Must be set **before** CREATE PROCEDURE/FUNCTION/VIEW

## Examples

### Bad

```sql
-- No ANSI_WARNINGS setting (uses session default, may be OFF)
CREATE PROCEDURE dbo.ProcessData AS
BEGIN
    SELECT SUM(Amount) FROM dbo.Orders;
END;
GO

-- ANSI_WARNINGS set to OFF
SET ANSI_WARNINGS OFF;
GO
CREATE PROCEDURE dbo.ProcessData AS
BEGIN
    SELECT SUM(Amount) FROM dbo.Orders;
END;
GO
```

### Good

```sql
-- Set ANSI_WARNINGS ON at the beginning of script
SET ANSI_WARNINGS ON;
GO

CREATE PROCEDURE dbo.ProcessData AS
BEGIN
    SELECT SUM(Amount) FROM dbo.Orders;
END;
GO

-- Complete script with all recommended settings
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_WARNINGS ON;
SET ANSI_PADDING ON;
GO

CREATE PROCEDURE dbo.CalculateTotals AS
BEGIN
    SELECT CategoryId, SUM(Price) AS TotalPrice
    FROM dbo.Products
    GROUP BY CategoryId;
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
    { "id": "set-ansi-warnings", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
