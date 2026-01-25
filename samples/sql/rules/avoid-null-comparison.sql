-- avoid-null-comparison rule examples
-- This rule detects NULL comparisons using = or <> instead of IS NULL/IS NOT NULL
-- These comparisons always evaluate to UNKNOWN in SQL's three-valued logic.

-- BAD: Using = NULL (always evaluates to UNKNOWN, never returns rows)
SELECT UserId, UserName
FROM dbo.Users
WHERE MiddleName = NULL;

-- BAD: Using <> NULL (always evaluates to UNKNOWN, never returns rows)
SELECT UserId, UserName
FROM dbo.Users
WHERE MiddleName <> NULL;

-- BAD: Using != NULL
SELECT ProductId, ProductName
FROM dbo.Products
WHERE DiscountPrice != NULL;

-- BAD: NULL comparison in UPDATE
UPDATE dbo.Users
SET Status = 'Incomplete'
WHERE Email = NULL;

-- GOOD: Using IS NULL
SELECT UserId, UserName
FROM dbo.Users
WHERE MiddleName IS NULL;

-- GOOD: Using IS NOT NULL
SELECT UserId, UserName
FROM dbo.Users
WHERE MiddleName IS NOT NULL;

-- GOOD: IS NULL in UPDATE
UPDATE dbo.Users
SET Status = 'Incomplete'
WHERE Email IS NULL;

-- GOOD: COALESCE for default values
SELECT UserId, UserName, COALESCE(MiddleName, '') AS MiddleName
FROM dbo.Users;
