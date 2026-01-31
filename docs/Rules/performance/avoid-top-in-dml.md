# Avoid Top In Dml

**Rule ID:** `avoid-top-in-dml`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Disallows TOP in UPDATE/DELETE; it is frequently non-deterministic and easy to misuse without a carefully designed ordering strategy.

## Rationale

TOP in UPDATE/DELETE statements is **non-deterministic** when used without ORDER BY, and SQL Server **does not support ORDER BY in UPDATE/DELETE** (unlike SELECT). This creates serious problems:

1. **Unpredictable row selection**: Which 10 rows get updated? It depends on:
   - Physical storage order (changes with index maintenance)
   - Execution plan chosen by the optimizer
   - Parallel execution (different threads process different rows)
   - Page splits and fragmentation

2. **Non-reproducible results**: Running the same statement twice may update different rows

3. **Dangerous in production**:
   - Cannot verify which rows will be affected before execution
   - Cannot reproduce bugs reported in production
   - Cannot audit which records were modified

4. **Common misuse**: Developers expect TOP to work like SELECT TOP with ORDER BY, but it doesn't

**Better alternatives:**
- Use a WHERE clause with specific criteria
- Use a CTE with ROW_NUMBER() and ORDER BY for deterministic ordering
- Use EXISTS/NOT EXISTS subqueries for set-based logic

## Examples

### Bad

```sql
-- Which 10 users get updated? Unknown!
UPDATE TOP (10) users SET status = 'inactive';

-- Non-deterministic deletion
DELETE TOP (100) FROM logs;  -- Which 100 logs? Random!

-- Dangerous: May delete the wrong 5 orders
DELETE TOP (5) FROM orders WHERE customer_id = 123;

-- TOP with PERCENT is even worse (fractional rows)
UPDATE TOP (10) PERCENT products SET discount = 0.2;
```

### Good

```sql
-- Deterministic: Use specific criteria
UPDATE users
SET status = 'inactive'
WHERE last_login < DATEADD(YEAR, -1, GETDATE());

-- Deterministic: Use CTE with ROW_NUMBER() for ordered updates
WITH TopOrders AS (
    SELECT order_id,
           ROW_NUMBER() OVER (ORDER BY order_date DESC) AS rn
    FROM orders
    WHERE customer_id = 123
)
UPDATE o
SET status = 'cancelled'
FROM orders o
INNER JOIN TopOrders t ON o.order_id = t.order_id
WHERE t.rn <= 5;

-- Deterministic: Delete specific old records
DELETE FROM logs
WHERE created_at < DATEADD(DAY, -30, GETDATE());

-- Alternative: Use a subquery with specific IDs
DELETE FROM products
WHERE product_id IN (
    SELECT TOP 100 product_id
    FROM products
    ORDER BY created_at ASC  -- Oldest 100 products
);
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
    { "id": "avoid-top-in-dml", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
