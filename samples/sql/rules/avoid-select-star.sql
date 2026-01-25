-- avoid-select-star rule examples
-- This rule detects SELECT * usage which can cause performance issues
-- and maintenance problems when table schemas change.

-- BAD: Using SELECT * is discouraged
SELECT * FROM dbo.Users;

-- BAD: SELECT * in subquery
SELECT u.UserName
FROM (SELECT * FROM dbo.Users) u
WHERE u.IsActive = 1;

-- BAD: SELECT * with JOIN
SELECT *
FROM dbo.Orders o
INNER JOIN dbo.Customers c ON o.CustomerId = c.CustomerId;

-- GOOD: Explicitly list columns
SELECT UserId, UserName, Email, CreatedDate
FROM dbo.Users;

-- GOOD: Specific columns in JOIN
SELECT o.OrderId, o.OrderDate, c.CustomerName, c.Email
FROM dbo.Orders o
INNER JOIN dbo.Customers c ON o.CustomerId = c.CustomerId;
