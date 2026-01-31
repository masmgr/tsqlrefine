# Semantic Left Join Filtered By Where

**Rule ID:** `semantic/left-join-filtered-by-where`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects LEFT JOIN operations where the WHERE clause filters the right-side table, effectively making it an INNER JOIN.

## Rationale

LEFT JOIN is designed to preserve all rows from the left table, even when there's no match in the right table. However, **filtering the right table in the WHERE clause defeats this purpose** and effectively converts the LEFT JOIN into an INNER JOIN:

1. **LEFT JOIN produces NULLs**: When no match exists, right-side columns are NULL
2. **WHERE clause filters NULLs**: `WHERE t2.status = 1` excludes all NULL rows
3. **Result**: Only matching rows remain (same as INNER JOIN)

**Why this matters:**

- **Misleading code**: The LEFT JOIN suggests "keep all left rows", but the WHERE clause contradicts this
- **Hidden behavior change**: Future developers may not realize the LEFT JOIN is ineffective
- **Query optimizer confusion**: The optimizer cannot optimize as effectively
- **Maintenance errors**: Someone might move the filter to the ON clause, unexpectedly changing results

**Correct approaches:**

- **Use INNER JOIN** if you only want matching rows
- **Move filter to ON clause** if you want to filter before joining (preserves non-matching left rows)
- **Filter left table in WHERE** if you need to reduce the left side

## Examples

### Bad

```sql
-- LEFT JOIN becomes INNER JOIN due to WHERE clause
SELECT * FROM users u
LEFT JOIN profiles p ON u.id = p.user_id
WHERE p.status = 'active';  -- Excludes users without profiles!

-- Multiple right-table filters
SELECT * FROM customers c
LEFT JOIN orders o ON c.id = o.customer_id
WHERE o.order_date > '2024-01-01'
  AND o.total > 100;  -- Only customers with orders!

-- NULL check doesn't help
SELECT * FROM departments d
LEFT JOIN employees e ON d.id = e.department_id
WHERE e.salary IS NOT NULL;  -- Still excludes empty departments
```

### Good

```sql
-- Option 1: Use INNER JOIN (only customers with active profiles)
SELECT * FROM users u
INNER JOIN profiles p ON u.id = p.user_id
WHERE p.status = 'active';

-- Option 2: Move filter to ON clause (all users, active profiles only)
SELECT * FROM users u
LEFT JOIN profiles p ON u.id = p.user_id AND p.status = 'active';

-- Option 3: Filter left table in WHERE (all active users)
SELECT * FROM users u
LEFT JOIN profiles p ON u.id = p.user_id
WHERE u.status = 'active';

-- Correct: Filter after aggregation (all departments, count only active employees)
SELECT d.name, COUNT(e.id) AS active_employees
FROM departments d
LEFT JOIN employees e ON d.id = e.department_id AND e.status = 'active'
GROUP BY d.name;
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
    { "id": "semantic/left-join-filtered-by-where", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
