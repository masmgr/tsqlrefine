# Avoid Correlated Scalar Subquery In Select

**Rule ID:** `avoid-correlated-scalar-subquery-in-select`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Detects correlated scalar subqueries in the SELECT list which execute once per outer row and cause severe performance degradation. Non-correlated scalar subqueries and subqueries in WHERE or other clauses are not flagged.

## Rationale

Correlated scalar subqueries in the SELECT list execute once for every row returned by the outer query, leading to N+1 query patterns:

**Performance problems:**
- **Row-by-row execution**: The subquery runs once per outer row
- **Exponential cost**: Performance degrades linearly with outer row count
- **Hidden complexity**: The subquery cost is not immediately obvious from the query structure
- **Optimizer limitations**: SQL Server may not always be able to decorrelate these subqueries

**Solution:** Replace with JOIN, CROSS APPLY, or window functions. These alternatives allow SQL Server to process the data in bulk rather than row-by-row.

## Examples

### Bad

```sql
-- Correlated scalar subquery in SELECT
SELECT
    o.order_id,
    o.customer_id,
    (SELECT c.name FROM Customers c WHERE c.id = o.customer_id) AS customer_name
FROM Orders o;

-- Multiple correlated scalar subqueries
SELECT
    e.id,
    (SELECT d.name FROM Departments d WHERE d.id = e.dept_id) AS dept_name,
    (SELECT m.name FROM Employees m WHERE m.id = e.manager_id) AS manager_name
FROM Employees e;

-- Correlated subquery inside a function
SELECT
    o.order_id,
    ISNULL((SELECT c.name FROM Customers c WHERE c.id = o.customer_id), 'Unknown')
FROM Orders o;
```

### Good

```sql
-- Use JOIN instead
SELECT o.order_id, o.customer_id, c.name AS customer_name
FROM Orders o
JOIN Customers c ON c.id = o.customer_id;

-- Use CROSS APPLY for complex logic
SELECT o.order_id, ca.customer_name
FROM Orders o
CROSS APPLY (
    SELECT c.name AS customer_name
    FROM Customers c
    WHERE c.id = o.customer_id
) ca;

-- Non-correlated scalar subquery is OK
SELECT
    o.order_id,
    (SELECT MAX(id) FROM Customers) AS max_customer_id
FROM Orders o;
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
    { "id": "avoid-correlated-scalar-subquery-in-select", "enabled": false }
  ]
}
```

## See Also

- [prefer-exists-over-in-subquery](prefer-exists-over-in-subquery.md) - Recommends EXISTS over IN with subquery
- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
