# Duplicate Select Column

**Rule ID:** `duplicate-select-column`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects duplicate output column names in SELECT queries. Duplicate column names may cause ambiguous column references when the result is consumed by outer queries, views, or applications.

## Rationale

While SQL Server allows duplicate column names in SELECT results, they can lead to confusion and bugs. Outer queries referencing the duplicate column name will get unpredictable results. Applications consuming the result set may fail or return incorrect data when accessing columns by name. This rule uses Warning severity because some intermediate queries may intentionally produce duplicate columns.

Column name comparison is case-insensitive, matching SQL Server's default behavior. Columns without a deterministic name (e.g., expressions without aliases, SELECT *) are skipped.

## Examples

### Bad

```sql
-- Duplicate column names
SELECT id, name, id FROM Users;

-- Duplicate aliases
SELECT FirstName AS col, LastName AS col FROM Users;

-- Qualified columns with same base name
SELECT t.id, s.id
FROM dbo.Orders AS t
INNER JOIN dbo.OrderItems AS s ON t.id = s.order_id;
```

### Good

```sql
-- Unique column names
SELECT id, name, email FROM Users;

-- Unique aliases
SELECT FirstName AS first_name, LastName AS last_name FROM Users;

-- Qualified columns with unique aliases
SELECT t.id AS order_id, s.id AS item_id
FROM dbo.Orders AS t
INNER JOIN dbo.OrderItems AS s ON t.id = s.order_id;
```

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "duplicate-select-column", "enabled": false }
  ]
}
```

## See Also

- [duplicate-view-column](../schema/duplicate-view-column.md) - Detects duplicate columns in CREATE VIEW definitions (Error severity)
- [duplicate-table-function-column](../schema/duplicate-table-function-column.md) - Detects duplicate columns in table-valued function definitions (Error severity)
