# Require As For Table Alias

**Rule ID:** `require-as-for-table-alias`
**Category:** Style
**Severity:** Information
**Fixable:** Yes

## Description

Table aliases should use the AS keyword

## Rationale

Omitting `AS` keyword in table aliases makes code **less readable and inconsistent**:

1. **Readability**: `AS` makes the alias explicit
   - `FROM users u` looks like a syntax error at first glance
   - `FROM users AS u` clearly shows `u` is an alias

2. **Consistency**: Aligns with column alias syntax
   - If columns use `SELECT id AS userId`, tables should use `FROM users AS u`
   - Consistent style across all aliases improves code quality

3. **Clearer intent**: `AS` explicitly states "this is an alias"
   - New developers immediately understand the syntax
   - Code reviews are easier

4. **Standard SQL**: While optional in T-SQL, `AS` is more explicit and widely used

5. **Prevents confusion** with derived tables and subqueries
   - `FROM (SELECT ...) AS dt` requires `AS`, so using it everywhere is consistent

**Note**: This is a **style preference** - both forms are valid T-SQL, but `AS` improves consistency.

## Examples

### Bad

```sql
-- Missing AS
SELECT * FROM users u;

-- Multiple tables without AS (inconsistent with JOIN syntax)
SELECT * FROM users u
INNER JOIN orders o ON u.id = o.user_id;

-- Derived table with AS, but base table without (inconsistent)
SELECT * FROM users u
INNER JOIN (
    SELECT user_id, COUNT(*) AS order_count
    FROM orders
    GROUP BY user_id
) AS oc ON u.id = oc.user_id;  -- AS here, but not for 'u'

-- Complex query without AS (harder to read)
SELECT u.name, o.total
FROM users u, orders o
WHERE u.id = o.user_id;
```

### Good

```sql
-- With AS (explicit)
SELECT * FROM users AS u;

-- Multiple tables with AS (consistent)
SELECT * FROM users AS u
INNER JOIN orders AS o ON u.id = o.user_id;

-- All aliases use AS (consistent style)
SELECT * FROM users AS u
INNER JOIN (
    SELECT user_id, COUNT(*) AS order_count
    FROM orders
    GROUP BY user_id
) AS oc ON u.id = oc.user_id;

-- Complex query with AS (readable)
SELECT u.name, o.total
FROM users AS u
INNER JOIN orders AS o ON u.id = o.user_id;

-- Multiple JOINs with AS
SELECT
    c.company_name,
    o.order_date,
    od.quantity,
    p.product_name
FROM customers AS c
INNER JOIN orders AS o ON c.customer_id = o.customer_id
INNER JOIN order_details AS od ON o.order_id = od.order_id
INNER JOIN products AS p ON od.product_id = p.product_id;

-- Derived table with AS
SELECT dept_name, avg_salary
FROM (
    SELECT department_id, AVG(salary) AS avg_salary
    FROM employees
    GROUP BY department_id
) AS dept_stats
INNER JOIN departments AS d ON dept_stats.department_id = d.department_id;

-- CTE and table aliases both use AS
WITH active_users AS (
    SELECT user_id, username
    FROM users
    WHERE status = 'active'
)
SELECT au.username, o.order_date
FROM active_users AS au
INNER JOIN orders AS o ON au.user_id = o.user_id;
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
    { "id": "require-as-for-table-alias", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
