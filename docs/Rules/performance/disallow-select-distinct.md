# Disallow Select Distinct

**Rule ID:** `disallow-select-distinct`
**Category:** Performance
**Severity:** Information
**Fixable:** No

## Description

Flags `SELECT DISTINCT` usage which often masks JOIN bugs or missing GROUP BY, and has performance implications.

## Rationale

`DISTINCT` is frequently used as a "quick fix" for duplicate rows caused by incorrect JOINs, hiding the root cause instead of fixing it. This leads to:

- **Hidden bugs**: Incorrect JOIN logic that produces wrong row counts
- **Performance impact**: Adds implicit sort/hash operations
- **Maintenance issues**: Makes it harder to understand query intent
- **Data quality**: May hide 1:N relationship problems

In many cases, the correct solution is either:
- Fix the JOIN logic (add proper conditions, change JOIN type)
- Use `GROUP BY` with aggregation (when summarizing data)

## Examples

### Bad

```sql
-- DISTINCT hiding 1:N relationship bug
SELECT DISTINCT c.CustomerName, c.City
FROM Customers c
JOIN Orders o ON c.Id = o.CustomerId;

-- DISTINCT as a band-aid for cartesian product
SELECT DISTINCT p.ProductName
FROM Products p
CROSS JOIN Categories cat  -- Missing JOIN condition!
WHERE cat.CategoryName = 'Electronics';
```

### Good

```sql
-- Fix JOIN logic: Add aggregation
SELECT c.CustomerName, c.City, COUNT(*) as OrderCount
FROM Customers c
JOIN Orders o ON c.Id = o.CustomerId
GROUP BY c.CustomerName, c.City;

-- Or: Only join if needed
SELECT c.CustomerName, c.City
FROM Customers c
WHERE EXISTS (SELECT 1 FROM Orders o WHERE o.CustomerId = c.Id);

-- Fix cartesian product: Add proper JOIN
SELECT p.ProductName
FROM Products p
JOIN Categories cat ON p.CategoryId = cat.Id
WHERE cat.CategoryName = 'Electronics';
```

## Legitimate Uses

Some scenarios where `DISTINCT` is appropriate:

### Set Operations

```sql
-- Get unique values for a dropdown/filter
SELECT DISTINCT Country
FROM Customers
ORDER BY Country;
```

### Data Integration

```sql
-- Deduplicate during ETL from messy source
SELECT DISTINCT CustomerCode, CustomerName
FROM StagingTable
WHERE ImportBatchId = @BatchId;
```

### Existence Checks (though EXISTS is better)

```sql
-- Check if any active customers exist in region
SELECT DISTINCT 1
FROM Customers
WHERE Region = @Region AND Active = 1;

-- Better: Use EXISTS
IF EXISTS (SELECT 1 FROM Customers WHERE Region = @Region AND Active = 1)
    -- ...
```

## Common Patterns

### Pattern 1: Hidden 1:N Relationship

**Problem:**
```sql
-- Returns duplicate customers (one per order)
SELECT c.CustomerName, c.Email
FROM Customers c
JOIN Orders o ON c.Id = o.CustomerId;
```

**Bad Fix:** Add DISTINCT
```sql
SELECT DISTINCT c.CustomerName, c.Email
FROM Customers c
JOIN Orders o ON c.Id = o.CustomerId;
```

**Good Fix:** Remove unnecessary JOIN
```sql
-- If you don't need order data, don't join Orders
SELECT c.CustomerName, c.Email
FROM Customers c
WHERE EXISTS (SELECT 1 FROM Orders o WHERE o.CustomerId = c.Id);
```

### Pattern 2: Aggregation Needed

**Problem:**
```sql
SELECT DISTINCT c.CustomerName, o.OrderDate
FROM Customers c
JOIN Orders o ON c.Id = o.CustomerId;
```

**Good Fix:** Use GROUP BY
```sql
SELECT c.CustomerName, COUNT(*) as OrderCount, MAX(o.OrderDate) as LatestOrder
FROM Customers c
JOIN Orders o ON c.Id = o.CustomerId
GROUP BY c.CustomerName;
```

## Performance Impact

`DISTINCT` operations require either:
- **Sort**: Sorts all rows to identify duplicates (memory/tempdb intensive)
- **Hash Match**: Builds hash table of unique rows (memory intensive)

For large result sets, this can be expensive.

## Configuration

This rule starts with `Information` severity to avoid noise. Consider promoting to `Warning` after team adoption.

To disable this rule:

```json
{
  "ruleset": "custom-ruleset.json"
}
```

In `custom-ruleset.json`:

```json
{
  "rules": [
    { "id": "disallow-select-distinct", "enabled": false }
  ]
}
```

## Limitations

- **Does not flag** `COUNT(DISTINCT column)` - this is a different construct with valid use cases
- **Cannot detect** legitimate deduplication scenarios automatically
- Requires developer judgment on case-by-case basis

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Microsoft Documentation: SELECT - DISTINCT](https://docs.microsoft.com/en-us/sql/t-sql/queries/select-transact-sql#distinct)
