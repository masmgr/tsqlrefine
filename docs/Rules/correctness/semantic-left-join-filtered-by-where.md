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

## Detected Predicate Types

This rule detects the following predicate types on right-side tables in WHERE clauses:

- **Comparison operators**: `=`, `<>`, `>`, `<`, `>=`, `<=`
- **IN predicates**: `column IN (values)`
- **LIKE predicates**: `column LIKE pattern`
- **BETWEEN predicates**: `column BETWEEN x AND y`
- **NOT expressions**: `NOT (condition)` wrapping any of the above

## Exceptions

The following predicate types on right-side tables are **not** flagged as violations because they preserve LEFT JOIN semantics:

- **IS NULL**: `WHERE right_table.col IS NULL` — This is a common and valid pattern to find rows with no match (anti-join). NULL right-side columns are the expected result of LEFT JOIN when no match exists.
- **IS NOT NULL**: `WHERE right_table.col IS NOT NULL` — While this does filter out non-matching rows (similar to INNER JOIN), it is treated as an explicit intent to check for NULL and is not flagged.
- **OR conditions that can keep NULL-extended rows**: `WHERE right_table.col = 1 OR left_table.flag = 1` — The right-side predicate is not mandatory, so LEFT JOIN semantics may still be preserved.

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
