# Duplicate View Column

**Rule ID:** `duplicate-view-column`
**Category:** Schema
**Severity:** Error
**Fixable:** No

## Description

Detects duplicate column names in CREATE VIEW definitions. Duplicate columns always cause a runtime error in SQL Server.

## Rationale

A VIEW that produces duplicate column names in its output will fail at execution time. This can happen when the SELECT list contains duplicate column references, duplicate aliases, or when the explicit column list of the VIEW contains duplicates. Catching this at lint time prevents deployment failures.

Column name comparison is case-insensitive, matching SQL Server's default behavior.

## Examples

### Bad

```sql
-- Duplicate column in SELECT list
CREATE VIEW dbo.v_Users AS
SELECT id, name, id FROM dbo.Users;

-- Duplicate alias
CREATE VIEW dbo.v_Products AS
SELECT ProductName AS col, Price AS col FROM dbo.Products;

-- Duplicate in explicit column list
CREATE VIEW dbo.v_Orders (order_id, order_id) AS
SELECT id, customer_id FROM dbo.Orders;
```

### Good

```sql
-- All columns are unique
CREATE VIEW dbo.v_Users AS
SELECT id, name, email FROM dbo.Users;

-- Unique aliases
CREATE VIEW dbo.v_Products AS
SELECT ProductName AS name, Price AS price FROM dbo.Products;

-- Unique explicit column list
CREATE VIEW dbo.v_Orders (order_id, cust_id) AS
SELECT id, customer_id FROM dbo.Orders;
```

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "duplicate-view-column", "enabled": false }
  ]
}
```

## See Also

- [duplicate-column-definition](duplicate-column-definition.md) - Detects duplicate columns in CREATE TABLE definitions
- [duplicate-table-function-column](duplicate-table-function-column.md) - Detects duplicate columns in table-valued function definitions
- [duplicate-select-column](../correctness/duplicate-select-column.md) - Detects duplicate columns in SELECT queries
