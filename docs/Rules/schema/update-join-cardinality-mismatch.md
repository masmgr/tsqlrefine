# Update Join Cardinality Mismatch

**Rule ID:** `update-join-cardinality-mismatch`
**Category:** Schema
**Severity:** Warning
**Fixable:** No

## Description

Detects UPDATE...FROM...JOIN where the join may produce multiple rows per target row, causing non-deterministic updates.

## Rationale

When an `UPDATE...FROM...JOIN` statement joins the target table to another table with a one-to-many (1:N) or many-to-many (M:N) relationship, SQL Server arbitrarily picks one of the matching rows to apply the update. This is a common source of subtle bugs because:

- The result depends on internal storage order, which is not guaranteed
- The query may appear to work correctly in development but produce wrong results in production
- The behavior can change silently after index rebuilds, statistics updates, or plan changes

This rule uses schema information (primary keys, unique constraints, and unique indexes) to determine join cardinality. It flags joins where the joined table's columns in the ON clause are not covered by a uniqueness guarantee, meaning multiple rows could match each target row.

## Examples

### Bad

```sql
-- OrderItems has multiple rows per OrderId (1:N relationship)
UPDATE o SET o.Amount = oi.Quantity * 10
FROM dbo.Orders AS o
INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = o.OrderId;

-- LEFT JOIN with same 1:N issue
UPDATE o SET o.Status = 'logged'
FROM dbo.Orders AS o
LEFT JOIN dbo.OrderLog AS ol ON ol.OrderId = o.OrderId;

-- Neither side has unique join columns (M:N)
UPDATE o SET o.Status = 'has-log'
FROM dbo.Orders AS o
INNER JOIN dbo.OrderLog AS ol ON ol.Action = o.Status;

-- Self-join on non-unique column
UPDATE o1 SET o1.Amount = o2.Amount
FROM dbo.Orders AS o1
INNER JOIN dbo.Orders AS o2 ON o2.CustomerId = o1.CustomerId;
```

### Good

```sql
-- Customers.CustomerId is a primary key (N:1 — at most one match per order)
UPDATE o SET o.Status = c.Name
FROM dbo.Orders AS o
INNER JOIN dbo.Customers AS c ON c.CustomerId = o.CustomerId;

-- OrderSummary.OrderId is a primary key (1:1 — exactly one match per order)
UPDATE o SET o.Amount = s.TotalAmount
FROM dbo.Orders AS o
INNER JOIN dbo.OrderSummary AS s ON s.OrderId = o.OrderId;

-- Composite unique constraint covers both join columns
UPDATE oi SET oi.Quantity = oip.UnitPrice
FROM dbo.OrderItems AS oi
INNER JOIN dbo.OrderItemPricing AS oip
    ON oip.OrderId = oi.OrderId AND oip.ProductId = oi.ProductId;

-- Simple UPDATE without JOIN (no cardinality concern)
UPDATE dbo.Orders SET Status = 'done' WHERE OrderId = 1;

-- Derived table with aggregation (pre-aggregated to one row per key)
UPDATE o SET o.Amount = sub.Total
FROM dbo.Orders AS o
INNER JOIN (
    SELECT OrderId, SUM(Quantity) AS Total
    FROM dbo.OrderItems
    GROUP BY OrderId
) AS sub ON sub.OrderId = o.OrderId;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "update-join-cardinality-mismatch", "enabled": false }
  ]
}
```

## See Also

- [join-foreign-key-mismatch](join-foreign-key-mismatch.md) — Detects JOINs where FK columns point to a different table than the join target
- [TsqlRefine Rules Documentation](../README.md)
