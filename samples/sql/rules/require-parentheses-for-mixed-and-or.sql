-- require-parentheses-for-mixed-and-or rule examples
-- This rule detects mixed AND/OR operators at same precedence level
-- without explicit parentheses to prevent precedence confusion.
-- Note: AND has higher precedence than OR in SQL.

-- BAD: Mixed AND/OR without parentheses - precedence is unclear
SELECT UserId, UserName
FROM dbo.Users
WHERE IsActive = 1 AND Role = 'Admin' OR Role = 'Manager';
-- This actually means: (IsActive = 1 AND Role = 'Admin') OR (Role = 'Manager')
-- But was the intent: IsActive = 1 AND (Role = 'Admin' OR Role = 'Manager')?

-- BAD: Complex condition without parentheses
SELECT OrderId
FROM dbo.Orders
WHERE Status = 'Pending' AND TotalAmount > 1000 OR Priority = 'High';

-- BAD: Multiple OR with AND mixed in
SELECT ProductId
FROM dbo.Products
WHERE Category = 'Electronics' OR Category = 'Computers' AND InStock = 1;

-- GOOD: Explicit parentheses make intent clear
SELECT UserId, UserName
FROM dbo.Users
WHERE IsActive = 1 AND (Role = 'Admin' OR Role = 'Manager');

-- GOOD: Parentheses clarify precedence
SELECT OrderId
FROM dbo.Orders
WHERE (Status = 'Pending' AND TotalAmount > 1000) OR Priority = 'High';

-- GOOD: Multiple conditions properly grouped
SELECT ProductId
FROM dbo.Products
WHERE (Category = 'Electronics' OR Category = 'Computers') AND InStock = 1;

-- GOOD: Only AND operators - no parentheses needed
SELECT UserId
FROM dbo.Users
WHERE IsActive = 1 AND Role = 'Admin' AND Department = 'IT';

-- GOOD: Only OR operators - no parentheses needed
SELECT ProductId
FROM dbo.Products
WHERE Category = 'Books' OR Category = 'Music' OR Category = 'Movies';
