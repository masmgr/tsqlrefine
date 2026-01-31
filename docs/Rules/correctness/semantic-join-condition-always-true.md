# Semantic Join Condition Always True

**Rule ID:** `semantic/join-condition-always-true`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects JOIN conditions that are always true or likely incorrect, such as 'ON 1=1' or self-comparisons.

## Rationale

JOIN conditions that are always true (like `ON 1=1`) create **Cartesian products**, which are almost always unintentional and have severe consequences:

1. **Massive result sets**: Every row in the first table matches every row in the second table
   - 1,000 rows × 1,000 rows = 1,000,000 result rows
   - 10,000 rows × 10,000 rows = 100,000,000 result rows

2. **Performance degradation**:
   - Memory exhaustion from huge intermediate result sets
   - Exponential growth with multiple joins
   - Query timeouts and system instability

3. **Incorrect results**: The multiplied rows rarely represent meaningful business data

4. **Silent bugs**: The query executes successfully, but returns wrong data

**Common causes:**
- Forgot to add the actual join condition
- Intended to use CROSS JOIN but wrote INNER/LEFT JOIN instead
- Copy-paste error from WHERE-based filtering patterns

## Examples

### Bad

```sql
-- Cartesian product: Every user matched with every profile
SELECT * FROM users u
JOIN profiles p ON 1=1;  -- 100 users × 100 profiles = 10,000 rows!

-- Self-comparison: Always evaluates to true
SELECT * FROM orders o1
JOIN orders o2 ON o1.id = o1.id;  -- Should be o1.id = o2.parent_id

-- Typo: Column name mismatch
SELECT * FROM products pr
JOIN categories c ON pr.id = pr.id;  -- Should be pr.category_id = c.id
```

### Good

```sql
-- Proper join condition
SELECT * FROM users u
JOIN profiles p ON u.id = p.user_id;

-- Intentional Cartesian product (use CROSS JOIN)
SELECT * FROM users
CROSS JOIN roles;  -- Explicit: Generate all user-role combinations

-- Self-join with proper condition
SELECT * FROM orders o1
JOIN orders o2 ON o1.id = o2.parent_id;
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
    { "id": "semantic/join-condition-always-true", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
