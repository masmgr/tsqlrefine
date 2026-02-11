# Duplicate Table Variable Column

**Rule ID:** `duplicate-table-variable-column`
**Category:** Schema
**Severity:** Error
**Fixable:** No

## Description

Detects duplicate column names in DECLARE @table TABLE variable definitions. Duplicate columns always cause a runtime error in SQL Server.

## Rationale

Defining the same column name more than once in a DECLARE TABLE variable is always a bug. SQL Server will reject the statement with an error at execution time. This is analogous to duplicate columns in a CREATE TABLE statement, but applies to table variables declared with DECLARE @var TABLE (...).

Column name comparison is case-insensitive, matching SQL Server's default behavior.

## Examples

### Bad

```sql
-- Duplicate column in table variable
DECLARE @temp TABLE (
    id INT,
    name VARCHAR(50),
    id INT
);

-- Case-insensitive duplicate
DECLARE @results TABLE (
    Name VARCHAR(100),
    Price DECIMAL(10, 2),
    NAME VARCHAR(200)
);
```

### Good

```sql
-- All columns are unique
DECLARE @temp TABLE (
    id INT,
    name VARCHAR(50),
    age INT
);
```

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "duplicate-table-variable-column", "enabled": false }
  ]
}
```

## See Also

- [duplicate-column-definition](duplicate-column-definition.md) - Detects duplicate columns in CREATE TABLE definitions
- [duplicate-table-function-column](duplicate-table-function-column.md) - Detects duplicate columns in table-valued function definitions
