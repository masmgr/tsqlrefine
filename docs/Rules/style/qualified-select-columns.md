# Qualified Select Columns

**Rule ID:** `qualified-select-columns`
**Category:** Query Structure
**Severity:** Warning
**Fixable:** No

## Description

Requires qualifying columns in SELECT lists when multiple tables are referenced; prevents subtle 'wrong table' mistakes when column names overlap.

## Rationale

When selecting from multiple tables, **unqualified column names** create ambiguity and maintenance problems:

1. **Ambiguity when column names overlap**:
   - Both tables may have columns with the same name (e.g., `id`, `name`, `created_at`)
   - SQL Server picks one table arbitrarily (first match in table order)
   - Results may be wrong without any error message

2. **Fragile refactoring**:
   - Adding a column to one table can silently change which `id` is selected
   - Schema changes break queries in unexpected ways
   - Difficult to debug (query succeeds but returns wrong data)

3. **Readability issues**:
   - Hard to tell which table a column comes from
   - Code review is difficult
   - Maintenance requires looking up table schemas

4. **Future schema conflicts**:
   - Adding a column with the same name to another table breaks the query
   - Or worse, silently changes behavior

**Best practice**: Always qualify columns with table alias when multiple tables are involved.

## Examples

### Bad

```sql
-- Ambiguous: Which table's id?
SELECT id FROM users u
INNER JOIN orders o ON u.id = o.user_id;
-- Works now, but fragile

-- Both tables have 'name' column
SELECT name, status FROM users u
INNER JOIN profiles p ON u.id = p.user_id;
-- Which name? Users or profiles?

-- Multiple unqualified columns (very confusing)
SELECT id, name, email, status, created_at
FROM users u
INNER JOIN profiles p ON u.id = p.user_id
INNER JOIN orders o ON u.id = o.user_id;
-- From which tables?

-- Subtle bug: Wrong table's column
SELECT id, total FROM orders o
INNER JOIN order_items oi ON o.id = oi.order_id;
-- If both have 'id', which one is selected?
```

### Good

```sql
-- Clear: Explicitly from users table
SELECT u.id FROM users u
INNER JOIN orders o ON u.id = o.user_id;

-- Both columns qualified
SELECT u.name, p.bio FROM users u
INNER JOIN profiles p ON u.id = p.user_id;

-- All columns qualified (readable)
SELECT u.id, u.name, u.email, p.bio, p.avatar_url
FROM users u
INNER JOIN profiles p ON u.id = p.user_id;

-- Multiple tables (clear which column comes from where)
SELECT
    u.id AS user_id,
    u.name AS user_name,
    o.id AS order_id,
    o.total AS order_total,
    oi.quantity,
    oi.price
FROM users u
INNER JOIN orders o ON u.id = o.user_id
INNER JOIN order_items oi ON o.id = oi.order_id;

-- Complex query with multiple joins (qualification essential)
SELECT
    c.customer_id,
    c.company_name,
    o.order_id,
    o.order_date,
    od.product_id,
    p.product_name,
    od.quantity,
    od.unit_price
FROM customers c
INNER JOIN orders o ON c.customer_id = o.customer_id
INNER JOIN order_details od ON o.order_id = od.order_id
INNER JOIN products p ON od.product_id = p.product_id
WHERE o.order_date > '2024-01-01';

-- Single table (qualification not required by this rule)
SELECT id, name, email FROM users;
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
    { "id": "qualified-select-columns", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
