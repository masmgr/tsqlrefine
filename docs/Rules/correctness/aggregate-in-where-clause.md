# Aggregate In Where Clause

**Rule ID:** `aggregate-in-where-clause`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Detects aggregate functions used directly in WHERE clauses. SQL Server raises a runtime error when aggregate functions like COUNT, SUM, AVG, MIN, or MAX appear directly in a WHERE clause.

## Rationale

Aggregate functions operate on sets of rows, but the WHERE clause filters individual rows before grouping occurs. Using an aggregate in a WHERE clause is always a logical error that SQL Server rejects at compile time with the message: "An aggregate may not appear in the WHERE clause unless it is in a subquery contained in a HAVING clause or a select list, and the column being aggregated is an outer reference."

The correct approach is to use a HAVING clause (which filters after grouping) or to wrap the aggregate in a subquery. Detecting this statically saves a round-trip to the server.

## Examples

### Bad

```sql
-- COUNT(*) used directly in WHERE clause
SELECT * FROM Orders WHERE COUNT(*) > 5;

-- SUM in WHERE comparison
SELECT * FROM Products WHERE SUM(Price) > 100;

-- Multiple aggregates in WHERE
SELECT * FROM t WHERE COUNT(*) > 0 AND SUM(amount) > 10;

-- Aggregate in BETWEEN
SELECT * FROM t WHERE MIN(x) BETWEEN 1 AND 10;

-- Aggregate in IS NULL check
SELECT * FROM t WHERE SUM(x) IS NULL;
```

### Good

```sql
-- Aggregate in HAVING clause (correct usage)
SELECT Category, COUNT(*) AS ProductCount
FROM Products
GROUP BY Category
HAVING COUNT(*) > 5;

-- Aggregate in subquery within WHERE
SELECT * FROM Orders
WHERE TotalAmount > (SELECT AVG(TotalAmount) FROM Orders);

-- Aggregate in EXISTS subquery
SELECT * FROM Customers
WHERE EXISTS (
    SELECT 1 FROM Orders
    WHERE Orders.CustomerId = Customers.Id
    HAVING COUNT(*) > 3
);

-- No aggregates in WHERE (normal filtering)
SELECT * FROM Products WHERE Price > 50 AND Category = 'Electronics';
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "aggregate-in-where-clause", "enabled": false }
  ]
}
```

## See Also

- [group-by-column-mismatch](group-by-column-mismatch.md) - Detects SELECT columns not in GROUP BY or aggregate
- [having-column-mismatch](having-column-mismatch.md) - Detects HAVING columns not in GROUP BY or aggregate
- [TsqlRefine Rules Documentation](../README.md)
