# Upper Lower

**Rule ID:** `upper-lower`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Detects UPPER or LOWER functions applied to columns in WHERE, JOIN ON, or HAVING predicates which prevents index usage

## Rationale

Applying `UPPER()` or `LOWER()` to a column in WHERE/JOIN/HAVING clauses **prevents index usage** and forces table/index scans:

1. **Index cannot be used**: Indexes store the original column values, not the upper/lowercase transformed values
   - SQL Server must scan every row and apply the function
   - Even with an index on the column, it becomes useless

2. **Performance degradation**:
   - 1,000 rows: Noticeable slowdown
   - 1,000,000 rows: Query timeout likely
   - Scales linearly with table size (O(n) instead of O(log n))

3. **Non-SARGable predicate**: Search ARGument-able predicates allow index seeks, but function-wrapped columns don't

**Better alternatives:**
- **Case-insensitive collation**: Use a case-insensitive collation (e.g., `SQL_Latin1_General_CP1_CI_AS`)
- **Computed column with index**: Create a computed column for `UPPER(column)` and index it
- **Apply function to constant**: If comparing to a constant, transform the constant instead of the column

## Examples

### Bad

```sql
-- Index on username cannot be used (full table scan)
SELECT * FROM users
WHERE UPPER(username) = 'ADMIN';

-- LOWER in JOIN prevents index usage on both tables
SELECT *
FROM products p
JOIN categories c ON LOWER(p.category_name) = LOWER(c.name);

-- Function in HAVING (after aggregation, but still slow)
SELECT department, COUNT(*)
FROM employees
GROUP BY department
HAVING LOWER(department) = 'sales';

-- Multiple function calls compound the problem
SELECT *
FROM customers
WHERE UPPER(first_name) = 'JOHN'
  AND UPPER(last_name) = 'SMITH';
```

### Good

```sql
-- Apply function to the constant (index on username can be used)
SELECT * FROM users
WHERE username = 'ADMIN';  -- If collation is case-insensitive

-- Or transform the search value
SELECT * FROM users
WHERE username = UPPER('admin');  -- Index can be used if exact match

-- Use case-insensitive collation in comparison
SELECT * FROM users
WHERE username COLLATE SQL_Latin1_General_CP1_CI_AS = 'admin';

-- Create computed column with index (one-time cost)
ALTER TABLE users
ADD username_upper AS UPPER(username) PERSISTED;

CREATE INDEX IX_users_username_upper ON users(username_upper);

-- Query using the indexed computed column
SELECT * FROM users
WHERE username_upper = 'ADMIN';  -- Index seek!

-- For JOIN, ensure both columns use same case-insensitive collation
SELECT *
FROM products p
JOIN categories c ON p.category_name = c.name;  -- Collation handles case

-- Fix HAVING by filtering before aggregation
SELECT department, COUNT(*)
FROM employees
WHERE department = 'Sales'  -- Filter before grouping
GROUP BY department;
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
    { "id": "upper-lower", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
