# Semantic Undefined Alias

**Rule ID:** `semantic/undefined-alias`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Detects references to undefined table aliases in column qualifiers.

## Rationale

References to undefined table aliases cause compile-time errors.

**Compile-time errors**:
```
Msg 4104, Level 16, State 1
The multi-part identifier "x.active" could not be bound.
```
or
```
Msg 207, Level 16, State 1
Invalid column name 'x.active'.
```

**Why this fails**:

1. **Alias must be defined**: Every table alias used in column qualifiers (e.g., `x.column`) must be defined in the FROM clause
2. **No implicit resolution**: SQL Server will not guess which table you meant
3. **Compile-time validation**: Error is caught before execution, prevents deployment

**Common scenarios**:

1. **Typo in alias reference**: Using wrong alias name in WHERE/SELECT/JOIN
   ```sql
   SELECT u.id FROM users usr WHERE u.active = 1;  -- 'u' not defined, should be 'usr'
   ```

2. **Copy-paste errors**: Copied condition from another query with different alias
   ```sql
   SELECT o.OrderId FROM Orders ord WHERE o.CustomerId = 100;  -- 'o' not defined, should be 'ord'
   ```

3. **Missing alias definition**: Forgot to define alias in FROM clause
   ```sql
   SELECT u.id, u.name FROM users WHERE u.active = 1;  -- 'u' not defined (no alias in FROM)
   ```

4. **Wrong alias in JOIN condition**: Referenced undefined alias in ON clause
   ```sql
   SELECT * FROM users u
   JOIN orders o ON u.id = x.user_id;  -- 'x' not defined (should be 'o')
   ```

5. **Subquery alias confusion**: Using outer query alias in inner query
   ```sql
   SELECT u.id FROM users u
   WHERE EXISTS (
       SELECT 1 FROM orders WHERE user_id = u.id  -- OK: outer reference
                                AND o.status = 'Active'  -- Error: 'o' not defined in subquery
   );
   ```

**Valid alias references**:

- Alias must be defined in FROM clause before use
- Aliases are case-insensitive (`u` = `U` = `u`)
- Outer query aliases can be referenced in correlated subqueries
- Each query scope has its own alias namespace

## Examples

### Bad

```sql
-- Undefined alias in WHERE clause
SELECT u.id FROM users WHERE x.active = 1;  -- Error: 'x' not defined

-- Missing alias in FROM clause
SELECT u.id, u.name FROM users WHERE u.active = 1;  -- Error: 'u' not defined

-- Typo in alias (defined as 'usr', used as 'u')
SELECT u.id, u.name FROM users usr WHERE u.active = 1;  -- Error: 'u' not defined

-- Wrong alias in JOIN condition
SELECT * FROM users u
JOIN orders o ON u.id = x.user_id;  -- Error: 'x' not defined

-- Copy-paste error (alias from different query)
SELECT o.OrderId, o.Total
FROM Orders ord
WHERE o.CustomerId = 100;  -- Error: 'o' not defined (should be 'ord')

-- Undefined alias in SELECT list
SELECT u.id, o.OrderId
FROM users usr
JOIN orders o ON usr.id = o.user_id
WHERE u.active = 1;  -- Error: 'u' not defined (should be 'usr')

-- Subquery with undefined alias
SELECT u.id FROM users u
WHERE EXISTS (
    SELECT 1 FROM orders o
    WHERE o.user_id = u.id
      AND x.status = 'Active'  -- Error: 'x' not defined
);

-- Multiple undefined aliases
SELECT x.id, y.name
FROM users u
JOIN orders o ON u.id = o.user_id;  -- Error: 'x' and 'y' not defined
```

### Good

```sql
-- Properly defined alias
SELECT u.id FROM users u WHERE u.active = 1;

-- Consistent alias usage
SELECT u.id, u.name FROM users u WHERE u.active = 1;

-- Correct alias in JOIN
SELECT * FROM users u
JOIN orders o ON u.id = o.user_id;

-- All aliases properly defined
SELECT u.id, o.OrderId, o.Total
FROM users u
JOIN orders o ON u.id = o.user_id
WHERE u.active = 1;

-- Correlated subquery (outer alias 'u' is valid)
SELECT u.id FROM users u
WHERE EXISTS (
    SELECT 1 FROM orders o
    WHERE o.user_id = u.id
      AND o.status = 'Active'
);

-- Multiple tables with consistent aliases
SELECT u.id, o.OrderId, p.ProductName
FROM users u
JOIN orders o ON u.id = o.user_id
JOIN products p ON o.product_id = p.id
WHERE u.active = 1
  AND o.status = 'Pending'
  AND p.stock > 0;

-- Table without alias (direct table name)
SELECT id, name FROM users WHERE active = 1;  -- OK: no alias needed
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
    { "id": "semantic/undefined-alias", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
