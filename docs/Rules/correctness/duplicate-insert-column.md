# Duplicate Insert Column

**Rule ID:** `duplicate-insert-column`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Detects duplicate column names in INSERT column lists. Duplicate columns always cause a runtime error in SQL Server.

## Rationale

Specifying the same column name more than once in an INSERT column list (`INSERT INTO table (col, col) ...`) is always a bug. SQL Server will reject the statement with an error at execution time. Catching this at lint time prevents deployment failures and avoids wasted troubleshooting effort.

Column name comparison is case-insensitive, matching SQL Server's default behavior.

## Examples

### Bad

```sql
-- Column 'id' specified twice
INSERT INTO dbo.Users (id, name, id)
VALUES (1, 'John', 2);

-- Case-insensitive duplicate
INSERT INTO dbo.Products (Name, Price, NAME)
VALUES ('Widget', 9.99, 'Gadget');

-- Duplicate in INSERT...SELECT
INSERT INTO dbo.Orders (order_id, customer_id, order_id)
SELECT id, cust_id, id FROM staging.Orders;
```

### Good

```sql
-- All columns are unique
INSERT INTO dbo.Users (id, name, email)
VALUES (1, 'John', 'john@example.com');

-- No column list (checked by require-column-list-for-insert-values instead)
INSERT INTO dbo.Users
VALUES (1, 'John', 'john@example.com');
```

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "duplicate-insert-column", "enabled": false }
  ]
}
```

## See Also

- [duplicate-column-definition](../schema/duplicate-column-definition.md) - Detects duplicate columns in CREATE TABLE definitions
- [duplicate-select-column](duplicate-select-column.md) - Detects duplicate output column names in SELECT queries
- [require-column-list-for-insert-values](require-column-list-for-insert-values.md) - Requires explicit column lists for INSERT VALUES
