# Duplicate Table Function Column

**Rule ID:** `duplicate-table-function-column`
**Category:** Schema
**Severity:** Error
**Fixable:** No

## Description

Detects duplicate column names in table-valued function definitions. Applies to both inline table-valued functions (SELECT list) and multi-statement table-valued functions (return table column definitions). Duplicate columns always cause a runtime error.

## Rationale

A table-valued function that produces duplicate column names in its output will fail at execution time. For inline TVFs, this occurs when the RETURN SELECT list has duplicate columns or aliases. For multi-statement TVFs, this occurs when the RETURNS @table TABLE definition contains duplicate column names. Catching this at lint time prevents deployment failures.

Column name comparison is case-insensitive, matching SQL Server's default behavior.

## Examples

### Bad

```sql
-- Inline TVF with duplicate column
CREATE FUNCTION dbo.fn_GetUsers()
RETURNS TABLE
AS
RETURN (SELECT id, name, id FROM dbo.Users);

-- Multi-statement TVF with duplicate column definition
CREATE FUNCTION dbo.fn_GetOrders()
RETURNS @result TABLE (order_id INT, customer_id INT, order_id INT)
AS
BEGIN
    RETURN;
END;
```

### Good

```sql
-- Inline TVF with unique columns
CREATE FUNCTION dbo.fn_GetUsers()
RETURNS TABLE
AS
RETURN (SELECT id, name, email FROM dbo.Users);

-- Multi-statement TVF with unique columns
CREATE FUNCTION dbo.fn_GetOrders()
RETURNS @result TABLE (order_id INT, customer_id INT, order_date DATE)
AS
BEGIN
    RETURN;
END;
```

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "duplicate-table-function-column", "enabled": false }
  ]
}
```

## See Also

- [duplicate-view-column](duplicate-view-column.md) - Detects duplicate columns in CREATE VIEW definitions
- [duplicate-column-definition](duplicate-column-definition.md) - Detects duplicate columns in CREATE TABLE definitions
- [duplicate-table-variable-column](duplicate-table-variable-column.md) - Detects duplicate columns in table variable definitions
