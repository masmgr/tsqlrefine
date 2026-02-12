# Group By Column Mismatch

**Rule ID:** `group-by-column-mismatch`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects SELECT columns not contained in GROUP BY or an aggregate function. When a query uses GROUP BY, every column in the SELECT list must either appear in the GROUP BY clause or be wrapped in an aggregate function (COUNT, SUM, AVG, MIN, MAX, etc.). SQL Server raises error 8120 at runtime for such violations.

## Rationale

Including a non-aggregated column that is not part of the GROUP BY clause is a common mistake that always results in a runtime error. Unlike MySQL's permissive behavior, SQL Server strictly enforces this rule. Detecting this statically saves a round-trip to the server and catches the error earlier in the development cycle.

The rule handles qualified and unqualified column references flexibly: `t.a` in SELECT matches `a` in GROUP BY (and vice versa). Columns inside aggregate functions, window functions (with OVER clause), scalar subqueries, and constant literals are correctly excluded from the check.

## Examples

### Bad

```sql
-- Column 'b' is not in GROUP BY or an aggregate function
SELECT a, b
FROM Orders
GROUP BY a;

-- Qualified column not in GROUP BY
SELECT t.OrderId, t.CustomerName
FROM Orders AS t
GROUP BY t.OrderId;

-- Column in expression not in GROUP BY
SELECT Category, Price + Tax AS TotalPrice
FROM Products
GROUP BY Category;

-- Column inside CASE not in GROUP BY
SELECT Status,
       CASE WHEN Amount > 100 THEN Amount ELSE 0 END AS HighAmount
FROM Orders
GROUP BY Status;

-- Column in non-aggregate function not in GROUP BY
SELECT Region, UPPER(City) AS CityUpper
FROM Stores
GROUP BY Region;
```

### Good

```sql
-- All columns in GROUP BY
SELECT a, b
FROM Orders
GROUP BY a, b;

-- Column wrapped in aggregate function
SELECT Category, COUNT(ProductId) AS ProductCount
FROM Products
GROUP BY Category;

-- Multiple aggregate functions
SELECT Department,
       COUNT(*) AS EmpCount,
       AVG(Salary) AS AvgSalary,
       MAX(HireDate) AS LatestHire
FROM Employees
GROUP BY Department;

-- Constant literal (no GROUP BY needed)
SELECT Category, 1 AS Flag
FROM Products
GROUP BY Category;

-- Scalar subquery (independent scope)
SELECT Category, (SELECT MAX(Price) FROM Products AS p2)
FROM Products
GROUP BY Category;

-- Window function (OVER clause)
SELECT Category,
       ROW_NUMBER() OVER (ORDER BY Category) AS RowNum
FROM Products
GROUP BY Category;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "group-by-column-mismatch", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
