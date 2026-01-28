-- no-top-without-order-by-in-select-into rule examples
-- This rule detects SELECT TOP ... INTO without ORDER BY

-- BAD: TOP without ORDER BY in SELECT INTO
SELECT TOP 100 *
INTO dbo.TopCustomers
FROM dbo.Customers;

-- BAD: TOP with percent without ORDER BY
SELECT TOP 10 PERCENT OrderId, CustomerId, OrderDate
INTO #RecentOrders
FROM dbo.Orders;

-- BAD: TOP in SELECT INTO to temp table
SELECT TOP 1000 ProductId, ProductName, Price
INTO ##GlobalTempProducts
FROM dbo.Products;

-- GOOD: TOP with ORDER BY in SELECT INTO
SELECT TOP 100 *
INTO dbo.TopCustomers
FROM dbo.Customers
ORDER BY Revenue DESC;

-- GOOD: TOP with multiple ORDER BY columns
SELECT TOP 1000 OrderId, CustomerId, OrderDate
INTO #RecentOrders
FROM dbo.Orders
ORDER BY OrderDate DESC, OrderId DESC;

-- GOOD: TOP with percent and ORDER BY
SELECT TOP 10 PERCENT ProductId, ProductName, Price
INTO ##GlobalTempProducts
FROM dbo.Products
ORDER BY Price DESC;

-- GOOD: Regular SELECT TOP without INTO (different rule)
SELECT TOP 100 *
FROM dbo.Customers;

-- GOOD: SELECT INTO without TOP (no non-determinism issue)
SELECT OrderId, CustomerId, OrderDate
INTO #AllOrders
FROM dbo.Orders
WHERE OrderDate >= '2024-01-01';
