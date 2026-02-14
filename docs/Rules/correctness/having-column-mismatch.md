# Having Column Mismatch

**Rule ID:** `having-column-mismatch`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects columns in HAVING clause that are not contained in the GROUP BY clause or wrapped in an aggregate function. When a query uses GROUP BY, every column referenced in the HAVING clause must either appear in the GROUP BY clause or be inside an aggregate function (COUNT, SUM, AVG, MIN, MAX, etc.). SQL Server raises error 8120 at runtime for such violations.

## Rationale

This rule is the HAVING-clause counterpart of `group-by-column-mismatch`. While the GROUP BY rule checks the SELECT list, this rule covers the same class of error in the HAVING clause. A non-aggregated column in HAVING that is not part of GROUP BY is a common mistake that always results in a runtime error. Detecting this statically saves a round-trip to the server.

The rule handles qualified and unqualified column references flexibly: `t.a` in HAVING matches `a` in GROUP BY (and vice versa). Columns inside aggregate functions, scalar subqueries, and constant literals are correctly excluded from the check.

## Examples

### Bad

```sql
-- Column 'b' in HAVING is not in GROUP BY
SELECT a FROM Orders GROUP BY a HAVING b > 5;

-- Qualified column not in GROUP BY
SELECT t.a FROM Orders AS t GROUP BY t.a HAVING t.b > 100;

-- Multiple non-aggregated columns in HAVING
SELECT a FROM Products GROUP BY a HAVING b > 5 AND c < 10;

-- Column in non-aggregate function not in GROUP BY
SELECT a FROM Stores GROUP BY a HAVING UPPER(b) = 'X';

-- Column in LIKE predicate not in GROUP BY
SELECT Region FROM Stores GROUP BY Region HAVING City LIKE 'New%';
```

### Good

```sql
-- HAVING column is in GROUP BY
SELECT Category, COUNT(*)
FROM Products
GROUP BY Category
HAVING Category LIKE 'A%';

-- Aggregate function in HAVING
SELECT Department, COUNT(*) AS EmpCount
FROM Employees
GROUP BY Department
HAVING COUNT(*) > 5;

-- Multiple aggregates in HAVING
SELECT Category, COUNT(*), AVG(Price)
FROM Products
GROUP BY Category
HAVING COUNT(*) > 5 AND AVG(Price) > 10;

-- GROUP BY column in expression
SELECT a FROM t GROUP BY a HAVING a + 0 > 5;

-- Subquery in HAVING (independent scope)
SELECT Category
FROM Products
GROUP BY Category
HAVING COUNT(*) > (SELECT AVG(cnt) FROM (SELECT COUNT(*) AS cnt FROM Products GROUP BY Category) sub);
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "having-column-mismatch", "enabled": false }
  ]
}
```

## See Also

- [group-by-column-mismatch](group-by-column-mismatch.md) - Detects SELECT columns not in GROUP BY or aggregate
- [aggregate-in-where-clause](aggregate-in-where-clause.md) - Detects aggregate functions in WHERE clauses
- [TsqlRefine Rules Documentation](../README.md)
