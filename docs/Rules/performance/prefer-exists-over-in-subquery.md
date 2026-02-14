# Prefer Exists Over In Subquery

**Rule ID:** `prefer-exists-over-in-subquery`
**Category:** Performance
**Severity:** Information
**Fixable:** No

## Description

Detects WHERE column IN (SELECT ...) patterns and recommends using EXISTS instead for better performance with large datasets.

## Rationale

When using `IN` with a subquery, SQL Server must evaluate the entire subquery result set before comparing. With `EXISTS`, the engine can short-circuit and stop as soon as the first matching row is found. For large datasets, `EXISTS` can be significantly more efficient. Additionally, `NOT IN` with a subquery that may return NULL values produces unexpected results (the entire predicate evaluates to UNKNOWN), whereas `NOT EXISTS` handles NULLs correctly.

This rule only flags `IN` with subqueries in predicate contexts (WHERE, JOIN ON, HAVING). The following patterns are **not** flagged:

- Value lists like `IN (1, 2, 3)` or `IN (@id1, @id2)`
- `IN` used outside predicate contexts (e.g., in a CASE expression within the SELECT list)
- Subqueries that include an `IS NOT NULL` check on the same column as the SELECT column — this indicates the developer is intentionally guarding against NULL values, which is the primary correctness concern with `IN` subqueries

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

-- IN with subquery in HAVING
SELECT DeptId, COUNT(*) AS Cnt
FROM Users
GROUP BY DeptId
HAVING DeptId IN (SELECT Id FROM ActiveDepartments);
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

-- IN with subquery guarded by IS NOT NULL (not flagged)
SELECT * FROM Users
WHERE Id IN (SELECT UserId FROM Orders WHERE UserId IS NOT NULL);

-- IN in SELECT list (not a predicate context, not flagged)
SELECT
    CASE WHEN Id IN (SELECT UserId FROM Admins) THEN 1 ELSE 0 END AS IsAdmin
FROM Users;
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
