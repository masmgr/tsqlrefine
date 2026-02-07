# Prefer Exists Over In Subquery

**Rule ID:** `prefer-exists-over-in-subquery`
**Category:** Performance
**Severity:** Information
**Fixable:** No

## Description

Detects WHERE column IN (SELECT ...) patterns and recommends using EXISTS instead for better performance with large datasets.

## Rationale

When using `IN` with a subquery, SQL Server must evaluate the entire subquery result set before comparing. With `EXISTS`, the engine can short-circuit and stop as soon as the first matching row is found. For large datasets, `EXISTS` can be significantly more efficient. Additionally, `NOT IN` with a subquery that may return NULL values produces unexpected results (the entire predicate evaluates to UNKNOWN), whereas `NOT EXISTS` handles NULLs correctly.

This rule only flags `IN` with subqueries in predicate contexts (WHERE, JOIN ON, HAVING). Value lists like `IN (1, 2, 3)` and `IN` used outside predicate contexts are not flagged.

## Examples

### Bad

```sql
-- IN with subquery in WHERE
SELECT * FROM Users
WHERE Id IN (SELECT UserId FROM Orders);

-- NOT IN with subquery (also risky with NULLs)
SELECT * FROM Users
WHERE Id NOT IN (SELECT UserId FROM Orders);

-- IN with subquery in JOIN condition
SELECT u.*
FROM Users u
INNER JOIN Departments d ON d.Id = u.DeptId
    AND u.Id IN (SELECT UserId FROM ActiveUsers);
```

### Good

```sql
-- EXISTS subquery
SELECT * FROM Users u
WHERE EXISTS (SELECT 1 FROM Orders o WHERE o.UserId = u.Id);

-- NOT EXISTS subquery
SELECT * FROM Users u
WHERE NOT EXISTS (SELECT 1 FROM BlockedUsers b WHERE b.UserId = u.Id);

-- IN with value list (not a subquery)
SELECT * FROM Users
WHERE Status IN ('Active', 'Pending', 'Verified');
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "prefer-exists-over-in-subquery", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
