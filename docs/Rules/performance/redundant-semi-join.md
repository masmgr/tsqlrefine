# Redundant Semi Join

**Rule ID:** `redundant-semi-join`
**Category:** Performance
**Severity:** Information
**Fixable:** No

## Description

Detects simple `IN` or `EXISTS` predicates that repeat an INNER JOIN to the same table and equivalent key.

## Rationale

An INNER JOIN already proves that a matching row exists. Repeating the same relationship as a semi-join adds query complexity and may add optimizer work without changing the result.

The rule is deliberately conservative. It does not report subqueries with extra filters, aggregation, TOP, DISTINCT, additional joins, negation, or OUTER/CROSS joins because those forms can add meaningful semantics.

## Examples

### Bad

```sql
SELECT o.OrderId
FROM dbo.Orders AS o
INNER JOIN dbo.Customers AS c ON c.CustomerId = o.CustomerId
WHERE o.CustomerId IN (SELECT c2.CustomerId FROM dbo.Customers AS c2);
```

### Good

```sql
SELECT o.OrderId
FROM dbo.Orders AS o
INNER JOIN dbo.Customers AS c ON c.CustomerId = o.CustomerId
WHERE EXISTS (
    SELECT 1
    FROM dbo.Customers AS c2
    WHERE c2.CustomerId = o.CustomerId
      AND c2.IsActive = 1
);
```

## Configuration

```json
{
  "rules": {
    "redundant-semi-join": "none"
  }
}
```

## See Also

- [prefer-exists-over-in-subquery](prefer-exists-over-in-subquery.md)
- [TsqlRefine Rules Documentation](../README.md)
