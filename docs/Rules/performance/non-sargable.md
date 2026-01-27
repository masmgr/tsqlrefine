# Non Sargable

**Rule ID:** `non-sargable`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Detects functions applied to columns in WHERE, JOIN ON, or HAVING predicates which prevents index usage (non-sargable predicates). This rule covers all functions except CAST/CONVERT, which are handled by the [avoid-implicit-conversion-in-predicate](avoid-implicit-conversion-in-predicate.md) rule.

## Rationale

When functions are applied to columns in predicates, SQL Server cannot use indexes efficiently (non-sargable):

**Performance problems:**
- **Index seek becomes index scan**: Functions on columns prevent index seeks, forcing full scans
- **Non-sargable predicates**: The optimizer cannot determine value ranges efficiently
- **Increased CPU usage**: Functions execute for every row in the table
- **Poor execution plans**: The optimizer cannot accurately estimate row counts

**Common violations:**
- String functions: `UPPER(column)`, `LOWER(column)`, `SUBSTRING(column, ...)`, `LTRIM(column)`, `RTRIM(column)`
- Date functions: `YEAR(column)`, `MONTH(column)`, `DAY(column)`
- Math functions: `ABS(column)`, `ROUND(column, ...)`

**Solution:** Use indexed computed columns, rewrite predicates, or apply functions to literal values instead.

## Examples

### Bad

```sql
-- LTRIM on column prevents index usage
SELECT * FROM users WHERE LTRIM(username) = 'admin';

-- UPPER on column - forces full scan
SELECT * FROM users WHERE UPPER(username) = 'ADMIN';

-- YEAR function on date column
SELECT * FROM orders WHERE YEAR(order_date) = 2023;

-- SUBSTRING in JOIN condition
SELECT * FROM users u
INNER JOIN profiles p ON SUBSTRING(u.username, 1, 5) = p.code;

-- Multiple functions
SELECT * FROM orders
WHERE YEAR(order_date) = 2023 AND MONTH(order_date) = 12;
```

### Good

```sql
-- Use direct column comparison with proper data
SELECT * FROM users WHERE username = 'admin';

-- For case-insensitive search, use collation
SELECT * FROM users WHERE username = 'ADMIN' COLLATE SQL_Latin1_General_CP1_CI_AS;

-- For date ranges, use direct comparison
SELECT * FROM orders
WHERE order_date >= '2023-01-01' AND order_date < '2024-01-01';

-- Use indexed computed column for frequently queried functions
ALTER TABLE users ADD username_upper AS UPPER(username) PERSISTED;
CREATE INDEX IX_Users_UsernameUpper ON users(username_upper);
SELECT * FROM users WHERE username_upper = 'ADMIN';

-- Direct column comparison in joins
SELECT * FROM users u
INNER JOIN profiles p ON u.username = p.code;
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
    { "id": "non-sargable", "enabled": false }
  ]
}
```

## See Also

- [avoid-implicit-conversion-in-predicate](avoid-implicit-conversion-in-predicate.md) - Detects CAST/CONVERT on columns
- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
