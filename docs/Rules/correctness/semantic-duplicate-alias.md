# Semantic Duplicate Alias

**Rule ID:** `semantic/duplicate-alias`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Detects duplicate table aliases in the same scope, which causes ambiguous references.

## Rationale

Duplicate table aliases cause compile-time errors and ambiguous column references.

**Compile-time error**:
```
Msg 1011, Level 16, State 1
The correlation name 'u' is specified multiple times in a FROM clause.
```

**Why duplicate aliases fail**:

1. **Ambiguous column references**: SQL Server cannot determine which table `u.id` refers to
2. **Correlation name conflict**: Each table alias must be unique within the same query scope
3. **Compile-time failure**: Query cannot execute, prevents deployment

**Common scenarios**:

1. **Copy-paste errors**: Duplicating JOIN clauses without renaming aliases
   ```sql
   SELECT * FROM users u
   JOIN orders u ON u.id = u.user_id  -- Copied 'u' by mistake
   ```

2. **Refactoring mistakes**: Adding new table without changing alias
   ```sql
   SELECT * FROM users u
   JOIN orders o ON u.id = o.user_id
   JOIN products u ON o.product_id = u.id  -- Reused 'u' accidentally
   ```

3. **Self-joins with same alias**: Forgetting to use different aliases for the same table
   ```sql
   SELECT * FROM users u
   JOIN users u ON u.manager_id = u.id  -- Both instances use 'u'
   ```

**SQL Server behavior**:

- **Compile error**: Query fails immediately, cannot execute
- **No workaround**: Must use unique aliases for each table in the FROM clause
- **Scope**: Duplicate aliases are only allowed in different query scopes (outer query vs subquery)

**Valid**: Different aliases in different scopes
```sql
SELECT * FROM users u  -- Outer query
WHERE id IN (
    SELECT user_id FROM orders u  -- Inner query (different scope, OK)
);
```

**Invalid**: Duplicate aliases in same scope
```sql
SELECT * FROM users u
JOIN orders u ON u.id = u.user_id;  -- Error!
```

## Examples

### Bad

```sql
-- Duplicate alias in JOIN (compile error)
SELECT * FROM users u JOIN orders u ON u.id = u.user_id;

-- Copy-paste error with same alias
SELECT u.Name, u.Total
FROM users u
JOIN (SELECT user_id, SUM(Amount) AS Total FROM orders GROUP BY user_id) u
ON u.id = u.user_id;  -- Error: 'u' used twice

-- Self-join with duplicate alias
SELECT manager.Name AS ManagerName, employee.Name AS EmployeeName
FROM users u  -- First 'u'
JOIN users u ON u.manager_id = u.id;  -- Second 'u' (error!)

-- Multiple duplicate aliases
SELECT *
FROM users u
JOIN orders u ON u.id = u.user_id      -- Duplicate 'u'
JOIN products p ON u.product_id = p.id
JOIN categories p ON p.category_id = p.id;  -- Duplicate 'p'
```

### Good

```sql
-- Unique alias for each table
SELECT * FROM users u JOIN orders o ON u.id = o.user_id;

-- Unique aliases with descriptive names
SELECT u.Name, orderTotals.Total
FROM users u
JOIN (SELECT user_id, SUM(Amount) AS Total FROM orders GROUP BY user_id) orderTotals
ON u.id = orderTotals.user_id;

-- Self-join with different aliases
SELECT manager.Name AS ManagerName, employee.Name AS EmployeeName
FROM users manager
JOIN users employee ON employee.manager_id = manager.id;

-- Multiple tables with unique aliases
SELECT *
FROM users u
JOIN orders o ON u.id = o.user_id
JOIN products p ON o.product_id = p.id
JOIN categories c ON p.category_id = c.id;

-- Same alias in different scopes (allowed)
SELECT * FROM users u  -- Outer query
WHERE id IN (
    SELECT user_id FROM orders u  -- Inner query (different scope, OK)
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
    { "id": "semantic-duplicate-alias", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
