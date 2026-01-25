-- require-column-list-for-insert-select rule examples
-- This rule requires INSERT SELECT statements to explicitly specify column list
-- to avoid errors when table schema changes.

-- BAD: INSERT SELECT without column list
INSERT INTO dbo.ArchivedOrders
SELECT * FROM dbo.Orders WHERE OrderDate < '2020-01-01';

-- BAD: INSERT SELECT with specific columns but no target column list
INSERT INTO dbo.UserBackup
SELECT UserId, UserName, Email FROM dbo.Users;

-- BAD: INSERT SELECT from JOIN without column list
INSERT INTO dbo.OrderSummary
SELECT o.OrderId, o.OrderDate, c.CustomerName, o.TotalAmount
FROM dbo.Orders o
INNER JOIN dbo.Customers c ON o.CustomerId = c.CustomerId
WHERE o.OrderDate >= '2024-01-01';

-- GOOD: Explicitly specify both target and source columns
INSERT INTO dbo.ArchivedOrders (OrderId, CustomerId, OrderDate, TotalAmount, Status)
SELECT OrderId, CustomerId, OrderDate, TotalAmount, Status
FROM dbo.Orders
WHERE OrderDate < '2020-01-01';

-- GOOD: Column list with specific columns
INSERT INTO dbo.UserBackup (UserId, UserName, Email)
SELECT UserId, UserName, Email
FROM dbo.Users;

-- GOOD: INSERT SELECT from JOIN with column list
INSERT INTO dbo.OrderSummary (OrderId, OrderDate, CustomerName, TotalAmount)
SELECT o.OrderId, o.OrderDate, c.CustomerName, o.TotalAmount
FROM dbo.Orders o
INNER JOIN dbo.Customers c ON o.CustomerId = c.CustomerId
WHERE o.OrderDate >= '2024-01-01';

-- GOOD: INSERT SELECT with derived columns
INSERT INTO dbo.MonthlySales (Year, Month, TotalSales, OrderCount)
SELECT
    YEAR(OrderDate) AS Year,
    MONTH(OrderDate) AS Month,
    SUM(TotalAmount) AS TotalSales,
    COUNT(*) AS OrderCount
FROM dbo.Orders
GROUP BY YEAR(OrderDate), MONTH(OrderDate);
