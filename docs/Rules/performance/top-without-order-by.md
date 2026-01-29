# Top Without Order By

**Rule ID:** `top-without-order-by`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Detects TOP clause without ORDER BY, which produces non-deterministic results.

## Rationale

TOP without ORDER BY returns unpredictable rows based on physical storage order. The query executes successfully, but results are non-deterministic, making this a code quality warning rather than a runtime error.

**Why results are unpredictable**:

SQL Server chooses rows based on physical storage order, which can change due to:
- Index reorganization/rebuild
- Page splits from INSERT/UPDATE operations
- Data modifications
- Parallel execution plans (different threads return rows in different order)

**Example - Non-deterministic results**:
```sql
-- Bad: Which 10 users? Changes between executions
SELECT TOP 10 * FROM Users;

-- Execution 1: Returns users with Id 1, 5, 7, 12, 15, 20, 23, 28, 31, 40
-- Execution 2: Returns users with Id 2, 6, 8, 13, 16, 21, 24, 29, 32, 41 (different!)
```

**Clustered index impact**:
```sql
-- Even with clustered index, no guarantee without ORDER BY
CREATE CLUSTERED INDEX IX_Users_Id ON Users(Id);
SELECT TOP 10 * FROM Users;  -- Still non-deterministic per SQL standard
-- (May follow index order, but not guaranteed by specification)
```

**When is TOP without ORDER BY acceptable?**
- Ad-hoc data inspection: `SELECT TOP 10 * FROM BigTable` (just need any sample)
- Performance testing: Getting sample rows for testing purposes
- Existence checks: `SELECT TOP 1 1 FROM Table WHERE ...` (only checking if rows exist)

**Best practice**: Always use ORDER BY with TOP for production code to ensure reproducible results.

## Examples

### Bad

```sql
-- Non-deterministic: which 10 users will be returned?
SELECT TOP 10 * FROM users;

-- Pagination without ORDER BY (results vary)
SELECT TOP 100 UserId, UserName FROM Users;

-- Sampling without ORDER BY (unreliable for consistent sampling)
SELECT TOP 5 PERCENT * FROM Orders;
```

### Good

```sql
-- Deterministic: always returns users with lowest 10 IDs
SELECT TOP 10 * FROM users ORDER BY id;

-- Pagination with ORDER BY (consistent results)
SELECT TOP 100 UserId, UserName FROM Users ORDER BY UserId;

-- Latest records (meaningful TOP)
SELECT TOP 10 * FROM Orders ORDER BY CreatedDate DESC;

-- Existence check (acceptable use of TOP without ORDER BY)
IF EXISTS (SELECT TOP 1 1 FROM Users WHERE Active = 1)
    PRINT 'Active users found';
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
    { "id": "top-without-order-by", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
