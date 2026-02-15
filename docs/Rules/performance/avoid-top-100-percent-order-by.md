# Avoid Top 100 Percent Order By

**Rule ID:** `avoid-top-100-percent-order-by`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Forbids TOP 100 PERCENT ORDER BY; it is redundant and often ignored by the optimizer.

## Rationale

`TOP 100 PERCENT` with `ORDER BY` is a legacy workaround from older SQL Server versions that is now **ineffective and wasteful**:

1. **Optimizer ignores it**: Modern SQL Server (2012+) query optimizer **removes** the ORDER BY clause from `TOP 100 PERCENT` queries, making it pointless

2. **Misleading code**: Developers expect the results to be ordered, but they won't be
   - In views: ORDER BY has no effect unless the outer query also uses ORDER BY or TOP
   - In subqueries: ORDER BY is removed unless TOP/OFFSET is present

3. **Performance overhead**: Even though the optimizer removes it, the query still needs parsing and optimization time

4. **Historical context**: This pattern was used to force ordering in views (SQL Server 2000/2005), but is now explicitly documented as unreliable

**Correct approaches:**
- Remove `TOP 100 PERCENT ORDER BY` entirely from views
- Apply `ORDER BY` in the outermost query that consumes the view
- Use actual `TOP N` or `OFFSET-FETCH` if you need ordering with row limiting

## Examples

### Bad

```sql
-- View with TOP 100 PERCENT ORDER BY (optimizer removes the ORDER BY)
CREATE VIEW vw_users AS
SELECT TOP 100 PERCENT *
FROM users
ORDER BY username;  -- This ORDER BY is ignored!

-- Subquery with pointless TOP 100 PERCENT
SELECT * FROM (
    SELECT TOP 100 PERCENT *
    FROM orders
    ORDER BY order_date DESC  -- Also ignored!
) AS ordered_orders;

-- Inline table-valued function (same problem)
CREATE FUNCTION fn_GetProducts()
RETURNS TABLE
AS
RETURN (
    SELECT TOP 100 PERCENT *
    FROM products
    ORDER BY product_name  -- Ignored by optimizer
);
```

### Good

```sql
-- View without ordering (apply ORDER BY in consuming query)
CREATE VIEW vw_users AS
SELECT *
FROM users;

-- Order when selecting from the view
SELECT * FROM vw_users
ORDER BY username;

-- Use actual TOP N with ORDER BY (respected by optimizer)
SELECT TOP 100 *
FROM orders
ORDER BY order_date DESC;

-- Use OFFSET-FETCH for pagination (ORDER BY is required and respected)
SELECT *
FROM products
ORDER BY product_name
OFFSET 0 ROWS
FETCH NEXT 50 ROWS ONLY;

-- If you need 100%, just SELECT without TOP
SELECT *
FROM users
ORDER BY username;  -- In outermost query, ORDER BY works
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
    { "id": "avoid-top-100-percent-order-by", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
