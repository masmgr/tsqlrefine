# Set Arithabort

**Rule ID:** `set-arithabort`
**Category:** Transactions
**Severity:** Warning
**Fixable:** No

## Description

Files should start with SET ARITHABORT ON within the first 10 statements.

## Rationale

Setting `ARITHABORT ON` at the beginning of scripts is **required for reliable arithmetic error handling** and is a **best practice** for SQL Server development:

1. **Required for indexed views and computed columns**:
   - SQL Server **requires** `ARITHABORT ON` when creating, modifying, or querying indexes on views or computed columns
   - Without it, queries on indexed views may fail or return incorrect results

2. **Arithmetic error behavior**:
   - `ARITHABORT ON` (recommended): Division by zero or arithmetic overflow terminates the query and rolls back the transaction
   - `ARITHABORT OFF`: Returns NULL for division by zero and may silently continue execution

3. **Query plan caching and performance**:
   - SQL Server Management Studio (SSMS) defaults to `ARITHABORT ON`
   - Many application connection drivers default to `ARITHABORT OFF`
   - This mismatch can cause **different query plans** for the same query, leading to unpredictable performance
   - A query that runs fast in SSMS may be slow in the application due to plan cache differences

4. **Environment consistency**:
   - Explicitly setting `ARITHABORT ON` eliminates plan cache pollution from mismatched settings
   - Ensures the same behavior in development (SSMS) and production (application)

5. **SET settings baked into objects**:
   - When creating stored procedures, functions, views, or triggers, the current SET options are **saved with the object**
   - Must be set **before** CREATE PROCEDURE/FUNCTION/VIEW

## Examples

### Bad

```sql
-- No ARITHABORT setting (uses session default, may be OFF)
CREATE PROCEDURE dbo.CalculateAverage AS
BEGIN
    SELECT AVG(Score) FROM dbo.Results;
END;
GO

-- ARITHABORT set to OFF
SET ARITHABORT OFF;
GO
CREATE PROCEDURE dbo.CalculateAverage AS
BEGIN
    -- Division by zero may silently return NULL
    SELECT Total / ItemCount FROM dbo.OrderSummary;
END;
GO
```

### Good

```sql
-- Set ARITHABORT ON at the beginning of script
SET ARITHABORT ON;
GO

CREATE PROCEDURE dbo.CalculateAverage AS
BEGIN
    SELECT AVG(Score) FROM dbo.Results;
END;
GO

-- Complete script with all recommended settings
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ARITHABORT ON;
SET XACT_ABORT ON;
GO

CREATE PROCEDURE dbo.ProcessPayment
    @OrderId INT,
    @Quantity INT
AS
BEGIN
    -- Division by zero will properly raise an error
    SELECT TotalAmount / @Quantity AS UnitPrice
    FROM dbo.Orders
    WHERE OrderId = @OrderId;
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
    { "id": "set-arithabort", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
