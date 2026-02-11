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
- Schema-qualified column references (e.g., `dbo.users.id`) are correctly recognized
- Table-valued function aliases (e.g., `JOIN dbo.fn_GetData() AS tvf`) are correctly recognized
- Temporary table aliases (e.g., `FROM #temp AS t`) are correctly recognized
- Table variable aliases (e.g., `FROM @tableVar AS tv`) are correctly recognized
- Built-in TVF aliases (e.g., `STRING_SPLIT(...) AS s`) are correctly recognized
- OPENJSON aliases (e.g., `OPENJSON(@json) AS j`) are correctly recognized
- PIVOT/UNPIVOT aliases are correctly recognized
- VALUES clause aliases (e.g., `VALUES (...) AS v(col)`) are correctly recognized
- APPLY function arguments (e.g., `CROSS APPLY OPENJSON(t.json_col)`) are validated
- DML OUTPUT/OUTPUT INTO qualifiers (e.g., `inserted.id`, `deleted.id`) are validated
- MERGE aliases in `USING`, `ON`, `WHEN`, and `OUTPUT` clauses are validated

**Subquery scope handling**:

This rule validates subqueries as separate scopes while supporting correlated references:

- **FROM clause subqueries**: Each derived table has its own scope
  ```sql
  SELECT sub.col FROM (SELECT t.id AS col FROM table1 t) AS sub;  -- OK
  ```
- **SELECT clause scalar subqueries**: Validated with their own scope
  ```sql
  SELECT (SELECT t.id FROM table1 t) FROM users u;  -- OK
  ```
- **WHERE clause subqueries (EXISTS, IN)**: Each subquery has its own scope
  ```sql
  SELECT u.id FROM users u WHERE EXISTS (SELECT 1 FROM orders o);  -- OK
  ```
- **Correlated subqueries**: Can reference outer query aliases
  ```sql
  SELECT u.id FROM users u WHERE EXISTS (SELECT 1 FROM orders o WHERE o.user_id = u.id);  -- OK: u.id references outer scope
  ```
- **UNION/INTERSECT/EXCEPT**: Each side is validated independently
  ```sql
  SELECT * FROM (SELECT t1.id FROM t1 UNION SELECT t2.id FROM t2) AS combined;  -- OK
  ```

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

-- Schema-qualified column references
SELECT dbo.users.id FROM dbo.users;  -- OK: table name 'users' is recognized

-- Fully-qualified column references (server.schema.table.column)
SELECT mydb.dbo.users.id FROM users;  -- OK: table name 'users' is recognized

-- Table-valued function with alias
SELECT tvf.id, tvf.name
FROM dbo.fn_GetUserData(@userId) AS tvf;  -- OK: alias 'tvf' is recognized

-- JOIN with table-valued function
SELECT u.id, tvf.data
FROM users u
JOIN dbo.fn_GetMetrics() AS tvf ON u.id = tvf.user_id;  -- OK: both aliases recognized

-- Temporary table with alias
SELECT t.id, t.name
FROM #temp_users AS t;  -- OK: alias 't' is recognized

-- Temporary table without alias (implicit name)
SELECT #temp.id FROM #temp;  -- OK: table name '#temp' is recognized

-- Table variable with alias
SELECT tv.id, tv.name
FROM @users AS tv;  -- OK: alias 'tv' is recognized

-- Table variable without alias (implicit name)
SELECT @tableVar.id FROM @tableVar;  -- OK: variable name '@tableVar' is recognized

-- Built-in table-valued function (STRING_SPLIT)
SELECT s.value
FROM STRING_SPLIT('a,b,c', ',') AS s;  -- OK: alias 's' is recognized

-- CROSS APPLY with STRING_SPLIT
SELECT t.id, s.value
FROM table1 t
CROSS APPLY STRING_SPLIT(t.csv, ',') AS s;  -- OK: both aliases recognized

-- APPLY argument qualifier validation
SELECT t.id, j.value
FROM table1 t
CROSS APPLY OPENJSON(t.payload) AS j;  -- OK: qualifier 't' in argument is recognized

-- OPENJSON with alias
SELECT j.[key], j.value
FROM OPENJSON(@json) AS j;  -- OK: alias 'j' is recognized

-- UPDATE OUTPUT pseudo-table qualifiers
UPDATE t
SET t.value = t.value + 1
OUTPUT inserted.id, deleted.id
FROM target t;  -- OK: inserted/deleted are recognized

-- MERGE source/target/output qualifiers
MERGE target t
USING source s ON t.id = s.id
WHEN MATCHED THEN UPDATE SET t.value = s.value
OUTPUT inserted.id, deleted.id, s.id;  -- OK: t/s/inserted/deleted are recognized

-- VALUES clause as table
SELECT v.id, v.name
FROM (VALUES (1, 'a'), (2, 'b')) AS v(id, name);  -- OK: alias 'v' is recognized

-- GENERATE_SERIES (SQL Server 2022+)
SELECT g.value
FROM GENERATE_SERIES(1, 10) AS g;  -- OK: alias 'g' is recognized
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
    { "id": "semantic-undefined-alias", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
