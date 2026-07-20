# Multi-Row Update From

**Rule ID:** `multi-row-update-from`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Warns on UPDATE...FROM with a JOIN, which can match multiple rows per target row and produce non-deterministic updates.

## Rationale

In SQL Server, `UPDATE ... FROM ... JOIN` allows the join to match multiple rows for a single target row. When that happens, SQL Server silently picks one of the matching rows (in an undefined order) to supply the update value — there is no error and no warning. The result is a non-deterministic update that can differ between runs, statistics, or server versions, making it one of the more dangerous, hard-to-diagnose pitfalls in T-SQL.

This rule flags the `UPDATE ... FROM ... JOIN` pattern itself, regardless of whether the join is actually unique or whether the target is a persistent table, temporary table, or table variable. This lets the author confirm that the join produces at most one row per target row even when no schema snapshot is available.

When a schema snapshot is available, the more precise [`update-join-cardinality-mismatch`](../schema/update-join-cardinality-mismatch.md) rule can also report joins whose cardinality is known to be unsafe from primary/foreign keys. This rule remains a schema-free syntactic guard so partially resolved or incomplete schema snapshots do not hide the broader `UPDATE ... FROM ... JOIN` risk.

## Examples

### Bad

```sql
-- The join to OrderItems can match many rows per Order,
-- so o.Amount is updated from an arbitrary matching row.
UPDATE o SET o.Amount = oi.Quantity * 10
FROM dbo.Orders AS o
INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = o.OrderId;

-- Temporary targets have the same non-deterministic behavior.
UPDATE w SET w.Amount = i.Amount
FROM #Work AS w
INNER JOIN dbo.OrderItems AS i ON i.OrderId = w.OrderId;
```

### Good

```sql
-- Simple UPDATE with no FROM/JOIN: deterministic.
UPDATE dbo.Orders SET Status = 'done' WHERE OrderId = 1;

-- UPDATE...FROM without a JOIN: single source, deterministic.
UPDATE o SET o.Status = 'pending'
FROM dbo.Orders AS o
WHERE o.Amount IS NULL;

-- Aggregate the source to one row per target key.
UPDATE w SET w.Amount = i.TotalAmount
FROM #Work AS w
INNER JOIN (
    SELECT OrderId, SUM(Amount) AS TotalAmount
    FROM dbo.OrderItems
    GROUP BY OrderId
) AS i ON i.OrderId = w.OrderId;
```

## Configuration

To disable this rule:

```json
{
  "rules": {
    "multi-row-update-from": "none"
  }
}
```

## See Also

- [update-join-cardinality-mismatch](../schema/update-join-cardinality-mismatch.md) — schema-aware precise detection
- [TsqlRefine Rules Documentation](../README.md)
