# Semantic Cte Name Conflict

**Rule ID:** `semantic/cte-name-conflict`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Detects duplicate CTE (Common Table Expression) names within the same WITH clause, which causes compile-time errors.

## Rationale

CTE names must be unique within a single WITH clause. Duplicate names cause immediate compile-time errors.

**Compile-time error**:
```
Msg 462, Level 16, State 1
Duplicate common table expression name 'UserCTE' was specified.
```

**Common scenarios**:

1. **Copy-paste errors**: Duplicating CTE definitions when building complex queries
   ```sql
   WITH UserCTE AS (...),
        UserCTE AS (...)  -- Accidentally pasted twice with same name
   ```

2. **Nested CTEs**: Accidentally reusing outer CTE name in inner scope (SQL Server doesn't allow this)
   ```sql
   WITH Outer AS (...)
   SELECT * FROM (
       WITH Outer AS (...)  -- Error: Name conflict with outer CTE
       SELECT * FROM Outer
   ) AS Derived;
   ```

3. **Refactoring mistakes**: Merging multiple queries without renaming CTEs
   ```sql
   -- Merging two queries with same CTE name
   WITH Results AS (SELECT * FROM Table1)  -- From query 1
   WITH Results AS (SELECT * FROM Table2)  -- From query 2 (conflict!)
   ```

**Valid**: Multiple CTEs with different names
```sql
WITH FirstCTE AS (...),
     SecondCTE AS (...),
     ThirdCTE AS (...)
SELECT * FROM FirstCTE JOIN SecondCTE JOIN ThirdCTE;
```

**Invalid**: Same CTE name twice
```sql
WITH UserCTE AS (...),
     UserCTE AS (...)  -- Compile error!
SELECT * FROM UserCTE;
```

## Examples

### Bad

```sql
-- Duplicate CTE name (compile error)
WITH UserCTE AS (SELECT 1 AS Id),
     UserCTE AS (SELECT 2 AS Id)  -- Error: Duplicate name
SELECT * FROM UserCTE;

-- Copy-paste error with same CTE name
WITH ActiveUsers AS (
    SELECT UserId, Name FROM Users WHERE Active = 1
),
ActiveUsers AS (  -- Duplicate!
    SELECT OrderId FROM Orders WHERE Status = 'Active'
)
SELECT * FROM ActiveUsers;

-- Refactoring mistake
WITH Results AS (SELECT * FROM Sales WHERE Year = 2023),
     Results AS (SELECT * FROM Sales WHERE Year = 2024)  -- Conflict!
SELECT * FROM Results;
```

### Good

```sql
-- Unique CTE names (valid)
WITH FirstCTE AS (SELECT 1 AS Id),
     SecondCTE AS (SELECT 2 AS Id)
SELECT * FROM FirstCTE
UNION ALL
SELECT * FROM SecondCTE;

-- Descriptive unique names
WITH ActiveUsers AS (
    SELECT UserId, Name FROM Users WHERE Active = 1
),
ActiveOrders AS (
    SELECT OrderId FROM Orders WHERE Status = 'Active'
)
SELECT u.*, o.OrderId
FROM ActiveUsers u
JOIN ActiveOrders o ON u.UserId = o.UserId;

-- Multiple year results with unique names
WITH Sales2023 AS (SELECT * FROM Sales WHERE Year = 2023),
     Sales2024 AS (SELECT * FROM Sales WHERE Year = 2024)
SELECT * FROM Sales2023
UNION ALL
SELECT * FROM Sales2024;
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
    { "id": "semantic/cte-name-conflict", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
