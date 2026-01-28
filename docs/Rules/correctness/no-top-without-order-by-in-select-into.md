# No Top Without Order By In Select Into

**Rule ID:** `no-top-without-order-by-in-select-into`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Detects `SELECT TOP ... INTO` statements without `ORDER BY` clause, which creates permanent tables with non-deterministic data.

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
  "rules": [
    { "id": "no-top-without-order-by-in-select-into", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [top-without-order-by](../performance/top-without-order-by.md) - Related rule for SELECT TOP in general
- [Microsoft Documentation: SELECT - ORDER BY Clause](https://docs.microsoft.com/en-us/sql/t-sql/queries/select-order-by-clause-transact-sql)
