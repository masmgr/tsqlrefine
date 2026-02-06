# Stuff Without Order By

**Rule ID:** `stuff-without-order-by`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects STUFF with FOR XML PATH that lacks ORDER BY, which may produce non-deterministic string concatenation results.

## Rationale

The `STUFF` function combined with `FOR XML PATH('')` is a common pattern for string aggregation in SQL Server versions prior to 2017. However, without an `ORDER BY` clause, the order of concatenated values is not guaranteed. This means:

1. **Non-deterministic results**: The same query may return values in different orders between executions
2. **Inconsistent behavior**: Results may vary across different SQL Server instances, hardware, or after index changes
3. **Hard-to-debug issues**: Intermittent failures in applications that depend on consistent ordering

SQL Server 2017+ users should consider using `STRING_AGG()` instead, which is cleaner and has explicit ordering support via `WITHIN GROUP (ORDER BY ...)`.

## Examples

### Bad

```sql
-- No ORDER BY - results may vary between executions
SELECT STUFF((SELECT ',' + name FROM users FOR XML PATH('')), 1, 1, '') AS names;

-- Correlated subquery without ORDER BY
SELECT
    p.id,
    STUFF((
        SELECT ', ' + c.category_name
        FROM categories c
        WHERE c.product_id = p.id
        FOR XML PATH('')
    ), 1, 2, '') AS categories
FROM products p;
```

### Good

```sql
-- With ORDER BY - deterministic results
SELECT STUFF((SELECT ',' + name FROM users ORDER BY name FOR XML PATH('')), 1, 1, '') AS names;

-- Correlated subquery with ORDER BY
SELECT
    p.id,
    STUFF((
        SELECT ', ' + c.category_name
        FROM categories c
        WHERE c.product_id = p.id
        ORDER BY c.category_name
        FOR XML PATH('')
    ), 1, 2, '') AS categories
FROM products p;

-- SQL Server 2017+: Use STRING_AGG instead
SELECT STRING_AGG(name, ',') WITHIN GROUP (ORDER BY name) AS names
FROM users;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "stuff-without-order-by", "enabled": false }
  ]
}
```

## See Also

- [prefer-string-agg-over-stuff](../style/prefer-string-agg-over-stuff.md) - Recommends STRING_AGG over STUFF for SQL Server 2017+
- [order-by-in-subquery](order-by-in-subquery.md) - Related rule for ORDER BY in subqueries
- [TsqlRefine Rules Documentation](../README.md)
