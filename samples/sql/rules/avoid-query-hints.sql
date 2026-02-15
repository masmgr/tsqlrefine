-- avoid-query-hints rule examples
-- This rule detects query and table hints that bypass the optimizer

-- BAD: Table hint - forcing index
SELECT OrderId, CustomerId
FROM Orders WITH (INDEX(IX_OrderDate))
WHERE CustomerId = @CustomerId;

-- BAD: Table hint - forcing seek
SELECT *
FROM LargeTable WITH (FORCESEEK)
WHERE Status = @Status;

-- BAD: Table hint - forcing scan
SELECT COUNT(*)
FROM Products WITH (FORCESCAN)
WHERE CategoryId = @CategoryId;

-- BAD: Table hint - NOEXPAND
SELECT *
FROM IndexedView WITH (NOEXPAND)
WHERE Region = @Region;

-- BAD: Query hint - FORCE ORDER
SELECT o.OrderId, c.CustomerName
FROM Orders o
JOIN Customers c ON o.CustomerId = c.CustomerId
OPTION (FORCE ORDER);

-- BAD: Query hint - RECOMPILE
SELECT *
FROM LargeTable
WHERE Status = @Status
OPTION (RECOMPILE);

-- BAD: Query hint - MAXDOP
SELECT COUNT(*)
FROM HugeTable
GROUP BY CategoryId
OPTION (MAXDOP 1);

-- BAD: Query hint - HASH JOIN
SELECT t1.Id, t2.Value
FROM Table1 t1
JOIN Table2 t2 ON t1.Id = t2.Id
OPTION (HASH JOIN);

-- BAD: Query hint - LOOP JOIN
SELECT p.ProductName, c.CategoryName
FROM Products p
JOIN Categories c ON p.CategoryId = c.CategoryId
OPTION (LOOP JOIN);

-- GOOD: No hints - let optimizer choose
SELECT OrderId, CustomerId
FROM Orders
WHERE CustomerId = @CustomerId;

-- GOOD: For parameter sniffing, use OPTIMIZE FOR UNKNOWN
SELECT *
FROM LargeTable
WHERE Status = @Status
OPTION (OPTIMIZE FOR UNKNOWN);

-- GOOD: Use local variable assignment for stable plans
DECLARE @LocalStatus VARCHAR(50) = @Status;
SELECT *
FROM LargeTable
WHERE Status = @LocalStatus;

-- GOOD: Split procedures for different scenarios
IF @Status = 'Active'
    EXEC dbo.GetActiveRecords;
ELSE
    EXEC dbo.GetOtherRecords @Status;

-- NOTE: NOLOCK hint is handled by avoid-nolock rule
-- This rule does not flag NOLOCK
SELECT * FROM Orders WITH (NOLOCK);
