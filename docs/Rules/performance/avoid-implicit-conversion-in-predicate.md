# Avoid Implicit Conversion in Predicate

**Rule ID:** `avoid-implicit-conversion-in-predicate`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Detects functions or conversions applied to columns in WHERE clauses, JOIN conditions, or HAVING clauses, which can prevent index usage and cause severe performance issues.

## Rationale

When functions or conversions are applied to columns in predicates, SQL Server cannot use indexes efficiently:

**Performance problems:**
- **Index seek becomes index scan**: Applying a function to a column prevents index seeks, forcing full index/table scans
- **Non-sargable predicates**: SQL Server's optimizer cannot determine the range of values to scan
- **Increased CPU usage**: Functions must be executed for every row in the table
- **Poor execution plans**: The optimizer cannot accurately estimate row counts

**Common violations:**
- `CAST(column AS ...)` or `CONVERT(column, ...)`
- `UPPER(column)`, `LOWER(column)`, `SUBSTRING(column, ...)`
- Date functions like `YEAR(column)`, `MONTH(column)`
- Mathematical operations like `column * 2`

**Solution:** Apply the function to the literal value instead, or use computed columns with indexes.

## Examples

### Bad

```sql
-- CONVERT on column prevents index usage
SELECT * FROM users WHERE CONVERT(VARCHAR(10), id) = '123';

-- CAST on column - index cannot be used efficiently
SELECT * FROM users WHERE CAST(id AS VARCHAR) = '123';

-- UPPER on column - forces full scan even with index on name
SELECT * FROM users WHERE UPPER(name) = 'JOHN';

-- SUBSTRING on column
SELECT * FROM users WHERE SUBSTRING(name, 1, 3) = 'Joh';

-- YEAR function on date column
SELECT * FROM orders WHERE YEAR(order_date) = 2023;

-- Function in JOIN condition - affects both tables
SELECT * FROM users u
JOIN orders o ON CAST(u.id AS VARCHAR) = o.user_id;

-- Multiple functions - multiple performance hits
SELECT * FROM users
WHERE UPPER(name) = 'JOHN' AND YEAR(created_date) = 2023;
```

### Good

```sql
-- Apply conversion to literal value, not column
SELECT * FROM users WHERE id = CAST('123' AS INT);

-- Use column directly with correct data type
SELECT * FROM users WHERE id = 123;

-- For case-insensitive search, use collation or case-insensitive index
SELECT * FROM users WHERE name = 'JOHN' COLLATE SQL_Latin1_General_CP1_CI_AS;

-- Or use computed column with index for complex functions
ALTER TABLE users ADD name_upper AS UPPER(name) PERSISTED;
CREATE INDEX IX_Users_NameUpper ON users(name_upper);
SELECT * FROM users WHERE name_upper = 'JOHN';

-- For date ranges, use direct comparison
SELECT * FROM orders
WHERE order_date >= '2023-01-01' AND order_date < '2024-01-01';

-- Direct column comparison in joins
SELECT * FROM users u
JOIN orders o ON u.id = o.user_id;

-- Apply function to both sides if necessary (better than one side)
SELECT * FROM users WHERE name = UPPER('john');
```

## Advanced Solutions

For complex scenarios where functions on columns are unavoidable:

```sql
-- Option 1: Indexed computed column (best for frequently queried functions)
ALTER TABLE users ADD created_year AS YEAR(created_date) PERSISTED;
CREATE INDEX IX_Users_CreatedYear ON users(created_year);
SELECT * FROM users WHERE created_year = 2023;

-- Option 2: Indexed view (for complex calculations)
CREATE VIEW vw_UsersByYear
WITH SCHEMABINDING
AS
SELECT user_id, YEAR(created_date) AS created_year
FROM dbo.users;
GO
CREATE UNIQUE CLUSTERED INDEX IX_UsersByYear ON vw_UsersByYear(user_id);
CREATE INDEX IX_Year ON vw_UsersByYear(created_year);
```

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "avoid-implicit-conversion-in-predicate", "enabled": false }
  ]
}
```

## See Also

- [non-sargable](non-sargable.md) - Detects functions applied to columns (excluding UPPER/LOWER/CAST/CONVERT)
- [upper-lower](upper-lower.md) - Specifically detects UPPER or LOWER functions on columns
