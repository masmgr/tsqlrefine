# Semantic Join Table Not Referenced in ON

**Rule ID:** `semantic/join-table-not-referenced-in-on`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects JOIN operations where the joined table is not referenced in the ON clause.

## Rationale

When a table is joined but not referenced in the ON clause, the join condition is incomplete or incorrect. This typically indicates one of these issues:

1. **Missing join condition**: The intended join column was forgotten, creating an implicit Cartesian product with the remaining condition
2. **Copy-paste error**: The condition was copied from elsewhere and references the wrong tables
3. **Logic error**: The developer intended to filter by the joined table but wrote the condition incorrectly

**Consequences:**
- The joined table's columns may all be the same value across result rows (if the condition is overly broad)
- Performance degradation due to unnecessary row multiplication
- Incorrect business logic results that are difficult to detect

**Target JOIN types:**
- `INNER JOIN`
- `LEFT OUTER JOIN` (and `LEFT JOIN`)
- `RIGHT OUTER JOIN` (and `RIGHT JOIN`)

**Excluded:**
- `CROSS JOIN`: Intentionally has no ON clause
- `FULL OUTER JOIN`: Not checked by this rule

## Examples

### Bad

```sql
-- Joined table 'c' is not referenced in ON clause
SELECT o.OrderId, o.CustomerId
FROM Orders o
INNER JOIN Customers c ON o.Status = 'Active';

-- Joined table alias 'b' is not referenced
SELECT a.col1, b.col2
FROM TableA a
LEFT JOIN TableB b ON a.col1 = 'value';

-- Self-reference on first table only (t2 not referenced)
SELECT *
FROM Products p
RIGHT JOIN Categories c ON p.Price > 100;

-- Multiple joins, both missing table references
SELECT e.Name, d.Name, m.Name
FROM Employees e
JOIN Departments d ON e.Status = 1
JOIN Managers m ON d.IsActive = 1;
```

### Good

```sql
-- Both tables referenced in ON clause
SELECT o.OrderId, c.CustomerName
FROM Orders o
INNER JOIN Customers c ON o.CustomerId = c.CustomerId;

-- Joined table referenced with additional condition
SELECT p.ProductName, c.CategoryName
FROM Products p
LEFT JOIN Categories c ON p.CategoryId = c.CategoryId AND c.IsActive = 1;

-- Multiple tables all properly referenced
SELECT e.Name, d.DeptName, m.ManagerName
FROM Employees e
JOIN Departments d ON e.DeptId = d.DeptId
JOIN Managers m ON d.ManagerId = m.ManagerId;

-- CROSS JOIN (no ON clause expected)
SELECT a.col, b.col
FROM TableA a
CROSS JOIN TableB b;

-- Joined table referenced via IS NOT NULL
SELECT *
FROM Orders o
LEFT JOIN OrderDetails od ON od.OrderId IS NOT NULL;

-- Joined table referenced in IN clause
SELECT *
FROM Products p
JOIN Categories c ON c.CategoryId IN (1, 2, 3);
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
    { "id": "semantic/join-table-not-referenced-in-on", "enabled": false }
  ]
}
```

## See Also

- [semantic/join-condition-always-true](semantic-join-condition-always-true.md) - Detects always-true conditions like `ON 1=1`
- [semantic/left-join-filtered-by-where](semantic-left-join-filtered-by-where.md) - Detects LEFT JOINs filtered by WHERE
- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
