# Avoid Implicit Conversion in Predicate

**Rule ID:** `avoid-implicit-conversion-in-predicate`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Detects CAST or CONVERT applied to columns in WHERE clauses, JOIN conditions, or HAVING clauses, which can cause implicit type conversions and prevent index usage.

## Rationale

When CAST or CONVERT is applied to columns in predicates, SQL Server cannot use indexes efficiently and may cause implicit type conversions:

**Performance problems:**
- **Index seek becomes index scan**: Type conversion on a column prevents index seeks, forcing full index/table scans
- **Implicit conversions**: SQL Server may perform hidden type conversions that degrade performance
- **Increased CPU usage**: Conversions must be executed for every row in the table
- **Poor execution plans**: The optimizer cannot accurately estimate row counts

**Common violations:**
- `CAST(column AS ...)` - Explicit conversion on indexed column
- `CONVERT(column, ...)` - Explicit conversion on indexed column

**Solution:** Apply the conversion to the literal value instead, ensure proper data types, or use computed columns with indexes.

**Note:** Other functions on columns (UPPER, LOWER, SUBSTRING, YEAR, etc.) are detected by the [non-sargable](non-sargable.md) rule.

## Examples

### Bad

```sql
-- CONVERT on column prevents index usage
SELECT * FROM users WHERE CONVERT(VARCHAR(10), id) = '123';

-- CAST on column - index cannot be used efficiently
SELECT * FROM users WHERE CAST(id AS VARCHAR) = '123';

-- CONVERT in JOIN condition - affects both tables
SELECT * FROM users u
JOIN orders o ON CAST(u.id AS VARCHAR) = o.user_id;

-- Multiple conversions - multiple performance hits
SELECT * FROM users
WHERE CAST(id AS VARCHAR) = '123' AND CONVERT(VARCHAR, user_id) = '456';
```

### Good

```sql
-- Apply conversion to literal value, not column
SELECT * FROM users WHERE id = CAST('123' AS INT);

-- Use column directly with correct data type
SELECT * FROM users WHERE id = 123;

-- Direct column comparison in joins
SELECT * FROM users u
JOIN orders o ON u.id = o.user_id;
```

## Advanced Solutions

For complex scenarios where type conversions on columns are unavoidable:

```sql
-- Option 1: Fix data type mismatches at the schema level
-- Instead of: WHERE CAST(id AS VARCHAR) = '123'
-- Change the comparison value's type:
SELECT * FROM users WHERE id = CAST('123' AS INT);

-- Option 2: Indexed computed column (for frequently queried conversions)
ALTER TABLE users ADD id_varchar AS CAST(id AS VARCHAR(20)) PERSISTED;
CREATE INDEX IX_Users_IdVarchar ON users(id_varchar);
SELECT * FROM users WHERE id_varchar = '123';
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

- [non-sargable](non-sargable.md) - Detects other functions applied to columns (UPPER, LOWER, SUBSTRING, YEAR, etc.)
