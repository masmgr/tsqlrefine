# Duplicate Column Definition

**Rule ID:** `duplicate-column-definition`
**Category:** Schema
**Severity:** Error
**Fixable:** No

## Description

Detects duplicate column names in CREATE TABLE definitions. Duplicate columns always cause a runtime error in SQL Server.

## Rationale

Defining the same column name more than once in a single CREATE TABLE statement is always a bug. SQL Server will reject the statement with an error at execution time. Catching this at lint time prevents deployment failures and avoids wasted troubleshooting effort.

Column name comparison is case-insensitive, matching SQL Server's default behavior.

## Examples

### Bad

```sql
-- Column 'id' defined twice
CREATE TABLE dbo.Users (
    id INT NOT NULL,
    name VARCHAR(50),
    id INT
);

-- Case-insensitive duplicate
CREATE TABLE dbo.Products (
    Name VARCHAR(100),
    Price DECIMAL(10, 2),
    NAME VARCHAR(200)
);
```

### Good

```sql
-- All columns are unique
CREATE TABLE dbo.Orders (
    order_id INT NOT NULL,
    customer_id INT,
    order_date DATE
);
```

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "duplicate-column-definition", "enabled": false }
  ]
}
```

## See Also

- [duplicate-index-column](duplicate-index-column.md) - Detects duplicate columns within an index definition
- [duplicate-foreign-key-column](duplicate-foreign-key-column.md) - Detects duplicate columns within a FOREIGN KEY constraint
