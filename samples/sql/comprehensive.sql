-- Comprehensive SQL sample demonstrating multiple tsqlrefine rules
-- This file contains both good and bad SQL practices for testing purposes.

-- =============================================================================
-- BAD PRACTICES (will trigger rules)
-- =============================================================================

-- Triggers: avoid-select-star
SELECT * FROM dbo.Users;

-- Triggers: dml-without-where
DELETE FROM dbo.TempLogs;
UPDATE dbo.UserPreferences SET Theme = 'dark';

-- Triggers: avoid-null-comparison
SELECT UserId, UserName
FROM dbo.Users
WHERE MiddleName = NULL;

-- Triggers: require-parentheses-for-mixed-and-or
SELECT OrderId
FROM dbo.Orders
WHERE Status = 'Pending' AND TotalAmount > 100 OR Priority = 'High';

-- Triggers: avoid-nolock
SELECT ProductId, ProductName, Price
FROM dbo.Products WITH (NOLOCK)
WHERE Category = 'Electronics';

-- Triggers: require-column-list-for-insert-values
INSERT INTO dbo.AuditLog
VALUES (1, 'UserLogin', GETDATE(), 'SUCCESS');

-- Triggers: require-column-list-for-insert-select
INSERT INTO dbo.ArchivedOrders
SELECT * FROM dbo.Orders WHERE YEAR(OrderDate) < 2020;

-- =============================================================================
-- GOOD PRACTICES (compliant with all rules)
-- =============================================================================

-- Compliant: Explicit column list in SELECT
SELECT UserId, UserName, Email, CreatedDate, IsActive
FROM dbo.Users
WHERE IsActive = 1;

-- Compliant: DELETE with WHERE clause
DELETE FROM dbo.TempLogs
WHERE CreatedDate < DATEADD(day, -7, GETDATE());

-- Compliant: UPDATE with WHERE clause
UPDATE dbo.UserPreferences
SET Theme = 'dark'
WHERE UserId IN (SELECT UserId FROM dbo.Users WHERE DarkModeEnabled = 1);

-- Compliant: IS NULL instead of = NULL
SELECT UserId, UserName, COALESCE(MiddleName, '') AS MiddleName
FROM dbo.Users
WHERE MiddleName IS NULL OR MiddleName = '';

-- Compliant: Parentheses for mixed AND/OR
SELECT OrderId, CustomerName, TotalAmount
FROM dbo.Orders
WHERE (Status = 'Pending' AND TotalAmount > 100) OR Priority = 'High';

-- Compliant: No NOLOCK hint
SELECT ProductId, ProductName, Price
FROM dbo.Products
WHERE Category = 'Electronics'
  AND InStock = 1;

-- Compliant: INSERT with column list
INSERT INTO dbo.AuditLog (LogId, EventType, EventDate, Status)
VALUES (1, 'UserLogin', GETDATE(), 'SUCCESS');

-- Compliant: INSERT SELECT with column lists
INSERT INTO dbo.ArchivedOrders (OrderId, CustomerId, OrderDate, TotalAmount, Status)
SELECT OrderId, CustomerId, OrderDate, TotalAmount, Status
FROM dbo.Orders
WHERE YEAR(OrderDate) < 2020;

-- =============================================================================
-- COMPLEX QUERIES (demonstrating good practices)
-- =============================================================================

-- Multi-table JOIN with explicit columns
SELECT
    o.OrderId,
    o.OrderDate,
    c.CustomerName,
    c.Email AS CustomerEmail,
    SUM(oi.Quantity * oi.UnitPrice) AS OrderTotal
FROM dbo.Orders o
INNER JOIN dbo.Customers c ON o.CustomerId = c.CustomerId
INNER JOIN dbo.OrderItems oi ON o.OrderId = oi.OrderId
WHERE o.OrderDate >= '2024-01-01'
  AND o.Status IN ('Completed', 'Shipped')
GROUP BY o.OrderId, o.OrderDate, c.CustomerName, c.Email
HAVING SUM(oi.Quantity * oi.UnitPrice) > 500
ORDER BY OrderTotal DESC;

-- Subquery with proper NULL handling
SELECT
    u.UserId,
    u.UserName,
    COALESCE(p.ProfilePicture, 'default.png') AS ProfilePicture
FROM dbo.Users u
LEFT JOIN dbo.UserProfiles p ON u.UserId = p.UserId
WHERE u.IsActive = 1
  AND p.ProfilePicture IS NOT NULL;

-- CTE (Common Table Expression) with clear column lists
WITH ActiveUsers AS (
    SELECT UserId, UserName, Email, LastLoginDate
    FROM dbo.Users
    WHERE IsActive = 1
      AND LastLoginDate >= DATEADD(month, -6, GETDATE())
),
UserOrders AS (
    SELECT
        o.CustomerId,
        COUNT(*) AS OrderCount,
        SUM(o.TotalAmount) AS TotalSpent
    FROM dbo.Orders o
    WHERE o.OrderDate >= DATEADD(year, -1, GETDATE())
    GROUP BY o.CustomerId
)
SELECT
    au.UserId,
    au.UserName,
    au.Email,
    COALESCE(uo.OrderCount, 0) AS OrderCount,
    COALESCE(uo.TotalSpent, 0) AS TotalSpent
FROM ActiveUsers au
LEFT JOIN UserOrders uo ON au.UserId = uo.CustomerId
ORDER BY TotalSpent DESC;

-- Stored procedure with proper practices
CREATE PROCEDURE dbo.GetOrdersByDateRange
    @StartDate DATE,
    @EndDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Validate parameters
    IF @StartDate IS NULL OR @EndDate IS NULL
    BEGIN
        RAISERROR('Start date and end date are required', 16, 1);
        RETURN;
    END

    -- Return results with explicit column list
    SELECT
        o.OrderId,
        o.OrderDate,
        c.CustomerName,
        o.TotalAmount,
        o.Status
    FROM dbo.Orders o
    INNER JOIN dbo.Customers c ON o.CustomerId = c.CustomerId
    WHERE o.OrderDate BETWEEN @StartDate AND @EndDate
      AND o.Status <> 'Cancelled'
    ORDER BY o.OrderDate DESC;
END;
GO
