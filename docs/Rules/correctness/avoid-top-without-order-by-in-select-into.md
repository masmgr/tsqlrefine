# Avoid Top Without Order By In Select Into

**Rule ID:** `avoid-top-without-order-by-in-select-into`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects `SELECT TOP ... INTO` statements without an `ORDER BY` clause, which may select non-deterministic rows.

For UNION, INTERSECT, and EXCEPT queries, each direct query branch containing `TOP` is checked. Constant `TOP 0` expressions are excluded because they select no rows. A constant `TOP 100 PERCENT` is also excluded because it selects all rows; variable percentages and other constants remain subject to the rule.

When a schema snapshot is available, a simple single-table query is not reported if equality
predicates cover a complete primary key, unique constraint, or unique index. Such a query returns
at most one row, so row ordering cannot change which row is persisted.

## Rationale

Unlike regular `SELECT TOP` (which is a runtime issue affecting result display), `SELECT TOP ... INTO` **persists non-deterministic data to storage**. This creates serious problems:

- **Reproducibility issues** in ETL/batch processing
- **Unpredictable results** across multiple executions
- **Data quality problems** in derived tables
- **Testing difficulties** - different data each run
- **Debugging nightmares** - cannot reproduce issues

Without `ORDER BY`, the database engine makes no guarantees about which rows are selected. The result depends on:
- Physical row order on disk
- Index selection by query optimizer
- Parallel query execution order
- Data modification history

## Examples

### Bad

```sql
-- Non-deterministic: Which 100 customers?
SELECT TOP 100 *
INTO dbo.TopCustomers
FROM Customers;

-- Non-deterministic: Which 1000 orders?
SELECT TOP 1000 OrderId, CustomerId, OrderDate
INTO #RecentOrders
FROM Orders;

-- TOP is evaluated in this UNION branch before the final set is produced
SELECT TOP 100 CustomerId
INTO #SelectedCustomers
FROM Customers
UNION ALL
SELECT CustomerId
FROM ArchivedCustomers;
```

### Good

```sql
-- Deterministic: Top 100 by revenue
SELECT TOP 100 *
INTO dbo.TopCustomers
FROM Customers
ORDER BY Revenue DESC;

-- Deterministic: 1000 most recent orders
SELECT TOP 1000 OrderId, CustomerId, OrderDate
INTO #RecentOrders
FROM Orders
ORDER BY OrderDate DESC, OrderId DESC;

-- All rows are selected, so row membership is deterministic
SELECT TOP (100) PERCENT OrderId, CustomerId
INTO #AllOrders
FROM Orders;

-- No rows are selected; this pattern is commonly used to create a table shape
SELECT TOP (0) OrderId, CustomerId
INTO #EmptyOrders
FROM Orders;

-- With schema analysis, the primary-key predicate proves that at most one row is returned
SELECT TOP (1) OrderId, CustomerId
INTO #SelectedOrder
FROM Orders
WHERE OrderId = @orderId;
```

## Common Patterns

### ETL Scenarios

**Bad:**
```sql
-- Daily snapshot - different data each run!
SELECT TOP 10000 *
INTO Archive.DailySnapshot
FROM Production.LargeTable;
```

**Good:**
```sql
-- Reproducible daily snapshot
SELECT TOP 10000 *
INTO Archive.DailySnapshot
FROM Production.LargeTable
ORDER BY CreatedDate DESC, Id DESC;
```

### Sampling for Analysis

**Bad:**
```sql
-- Non-reproducible sample
SELECT TOP 1 PERCENT *
INTO dbo.SampleData
FROM dbo.HugeTable;
```

**Good:**
```sql
-- Reproducible random sample (with seed)
SELECT TOP 1 PERCENT *
INTO dbo.SampleData
FROM dbo.HugeTable
ORDER BY NEWID();  -- Or use TABLESAMPLE with REPEATABLE
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
  "rules": {
    "avoid-top-without-order-by-in-select-into": "none"
  }
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [top-without-order-by](../performance/top-without-order-by.md) - Related rule for SELECT TOP in general
- [Microsoft Documentation: SELECT - ORDER BY Clause](https://docs.microsoft.com/en-us/sql/t-sql/queries/select-order-by-clause-transact-sql)
