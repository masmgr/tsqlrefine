# Disallow Cursors

**Rule ID:** `disallow-cursors`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Prohibit cursor usage; prefer set-based operations for better performance

## Rationale

Cursors cause severe performance degradation (often 100x-1000x slower than set-based operations).

**Why cursors are slow**:

1. **Row-by-row processing**: Processes one row at a time instead of operating on entire sets
   - **Set-based**: 1 million rows in 1 second (single operation)
   - **Cursor**: 1 million rows in 16+ minutes (1 million operations)
   - Performance degrades linearly with row count

2. **Optimizer cannot optimize**: Each FETCH is a separate operation
   - No query plan optimization across iterations
   - Cannot leverage indexes effectively
   - Cannot parallelize operations

3. **Resource overhead**:
   - **Locks held longer**: Row-level locks held during entire cursor loop (blocks other queries)
   - **TempDB usage**: Server-side cursors store result set in tempdb
   - **Memory consumption**: Cursor state maintained in memory
   - **Network roundtrips**: Each FETCH requires client-server communication (non-fast-forward cursors)

4. **Transaction issues**:
   - Long-running transactions (if cursor loop is wrapped in transaction)
   - Increased lock contention and deadlock risk
   - Log file growth (cannot checkpoint until transaction completes)

**Performance comparison example**:

```sql
-- Cursor approach (SLOW: ~16 minutes for 1M rows)
DECLARE @UserId INT;
DECLARE user_cursor CURSOR FOR SELECT UserId FROM Users;
OPEN user_cursor;
FETCH NEXT FROM user_cursor INTO @UserId;
WHILE @@FETCH_STATUS = 0
BEGIN
    UPDATE Orders SET ProcessedFlag = 1 WHERE UserId = @UserId;  -- 1 update per user
    FETCH NEXT FROM user_cursor INTO @UserId;
END;
CLOSE user_cursor;
DEALLOCATE user_cursor;

-- Set-based approach (FAST: ~1 second for 1M rows)
UPDATE o
SET ProcessedFlag = 1
FROM Orders o
JOIN Users u ON o.UserId = u.UserId;  -- Single update for all users
```

**Why SQL Server is designed for set-based operations**:

1. **Relational algebra**: SQL is based on set theory, not procedural iteration
2. **Query optimizer**: Optimizes entire statements, not individual row operations
3. **Index usage**: Indexes optimize set-based filtering/joining, not row-by-row access
4. **Parallel execution**: Set-based queries can parallelize; cursors cannot

**When cursors are acceptable** (rare):

1. **DDL operations**: Generating dynamic DDL for multiple databases/tables
   ```sql
   -- Iterating over databases to run DBCC CHECKDB
   DECLARE @DbName NVARCHAR(128);
   DECLARE db_cursor CURSOR FOR SELECT name FROM sys.databases WHERE state = 0;
   -- (acceptable: administrative task, not data processing)
   ```

2. **Row-by-row business logic**: Complex row-dependent calculations that cannot be expressed in SQL
   - WARNING: This is almost always solvable with set-based SQL (CTEs, window functions, etc.)
   - Only use cursor if truly impossible to convert to set-based approach

3. **Sequential processing requirements**: Operations that must process rows in specific order with dependencies
   - Example: Applying payment credits in chronological order with running balance

**Modern alternatives to cursors**:

1. **Window functions**: `ROW_NUMBER()`, `RANK()`, `SUM() OVER()`, etc.
2. **Recursive CTEs**: For hierarchical/sequential processing
3. **MERGE statement**: Complex insert/update/delete logic in single statement
4. **Table variables / temp tables**: For multi-step transformations
5. **CLR functions**: For truly complex procedural logic (better than cursors)

## Examples

### Bad

```sql
-- Cursor for row-by-row updates (VERY SLOW)
DECLARE @UserId INT, @UserName NVARCHAR(100);
DECLARE user_cursor CURSOR FOR SELECT UserId, UserName FROM Users;
OPEN user_cursor;
FETCH NEXT FROM user_cursor INTO @UserId, @UserName;
WHILE @@FETCH_STATUS = 0
BEGIN
    UPDATE Orders SET CustomerName = @UserName WHERE UserId = @UserId;
    FETCH NEXT FROM user_cursor INTO @UserId, @UserName;
END;
CLOSE user_cursor;
DEALLOCATE user_cursor;

-- Cursor for aggregations (SLOW)
DECLARE @ProductId INT, @Total DECIMAL(10,2);
DECLARE product_cursor CURSOR FOR SELECT ProductId FROM Products;
OPEN product_cursor;
FETCH NEXT FROM product_cursor INTO @ProductId;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @Total = SUM(Quantity * Price) FROM OrderDetails WHERE ProductId = @ProductId;
    UPDATE Products SET TotalSales = @Total WHERE ProductId = @ProductId;
    FETCH NEXT FROM product_cursor INTO @ProductId;
END;
CLOSE product_cursor;
DEALLOCATE product_cursor;

-- Cursor for inserts (SLOW)
DECLARE @OrderId INT, @Total DECIMAL(10,2);
DECLARE order_cursor CURSOR FOR SELECT OrderId, Total FROM Orders WHERE ProcessedFlag = 0;
OPEN order_cursor;
FETCH NEXT FROM order_cursor INTO @OrderId, @Total;
WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT INTO OrderHistory (OrderId, Total, ArchivedDate)
    VALUES (@OrderId, @Total, GETDATE());
    FETCH NEXT FROM order_cursor INTO @OrderId, @Total;
END;
CLOSE order_cursor;
DEALLOCATE order_cursor;

-- Cursor for conditional logic (SLOW)
DECLARE @CustomerId INT, @OrderCount INT;
DECLARE customer_cursor CURSOR FOR SELECT CustomerId FROM Customers;
OPEN customer_cursor;
FETCH NEXT FROM customer_cursor INTO @CustomerId;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @OrderCount = COUNT(*) FROM Orders WHERE CustomerId = @CustomerId;
    IF @OrderCount > 10
        UPDATE Customers SET VIPFlag = 1 WHERE CustomerId = @CustomerId;
    ELSE
        UPDATE Customers SET VIPFlag = 0 WHERE CustomerId = @CustomerId;
    FETCH NEXT FROM customer_cursor INTO @CustomerId;
END;
CLOSE customer_cursor;
DEALLOCATE customer_cursor;
```

### Good

```sql
-- Set-based update (FAST: single UPDATE statement)
UPDATE o
SET CustomerName = u.UserName
FROM Orders o
JOIN Users u ON o.UserId = u.UserId;

-- Set-based aggregation (FAST: single UPDATE with subquery)
UPDATE p
SET TotalSales = (
    SELECT SUM(Quantity * Price)
    FROM OrderDetails od
    WHERE od.ProductId = p.ProductId
)
FROM Products p;

-- Or using CTE for readability
WITH ProductTotals AS (
    SELECT ProductId, SUM(Quantity * Price) AS TotalSales
    FROM OrderDetails
    GROUP BY ProductId
)
UPDATE p
SET TotalSales = pt.TotalSales
FROM Products p
JOIN ProductTotals pt ON p.ProductId = pt.ProductId;

-- Set-based insert (FAST: single INSERT...SELECT)
INSERT INTO OrderHistory (OrderId, Total, ArchivedDate)
SELECT OrderId, Total, GETDATE()
FROM Orders
WHERE ProcessedFlag = 0;

-- Set-based conditional update (FAST: CASE expression)
UPDATE c
SET VIPFlag = CASE
    WHEN (SELECT COUNT(*) FROM Orders WHERE CustomerId = c.CustomerId) > 10 THEN 1
    ELSE 0
END
FROM Customers c;

-- Or using CTE for better performance
WITH CustomerOrderCounts AS (
    SELECT CustomerId, COUNT(*) AS OrderCount
    FROM Orders
    GROUP BY CustomerId
)
UPDATE c
SET VIPFlag = CASE WHEN coc.OrderCount > 10 THEN 1 ELSE 0 END
FROM Customers c
LEFT JOIN CustomerOrderCounts coc ON c.CustomerId = coc.CustomerId;

-- Window functions (FAST: for running totals, rankings)
-- Instead of cursor with running balance
SELECT
    TransactionId,
    Amount,
    SUM(Amount) OVER (ORDER BY TransactionDate, TransactionId) AS RunningBalance
FROM Transactions;

-- Recursive CTE (FAST: for hierarchical data)
-- Instead of cursor for employee hierarchy
WITH EmployeeHierarchy AS (
    -- Anchor: Top-level employees
    SELECT EmployeeId, ManagerId, Name, 1 AS Level
    FROM Employees
    WHERE ManagerId IS NULL

    UNION ALL

    -- Recursive: Subordinates
    SELECT e.EmployeeId, e.ManagerId, e.Name, eh.Level + 1
    FROM Employees e
    JOIN EmployeeHierarchy eh ON e.ManagerId = eh.EmployeeId
)
SELECT * FROM EmployeeHierarchy;

-- MERGE statement (FAST: complex upsert logic)
-- Instead of cursor with IF EXISTS checks
MERGE INTO TargetTable AS target
USING SourceTable AS source ON target.Id = source.Id
WHEN MATCHED THEN
    UPDATE SET target.Value = source.Value
WHEN NOT MATCHED THEN
    INSERT (Id, Value) VALUES (source.Id, source.Value)
WHEN NOT MATCHED BY SOURCE THEN
    DELETE;
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
    { "id": "disallow-cursors", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
