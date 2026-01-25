-- dml-without-where rule examples
-- This rule detects UPDATE/DELETE statements without WHERE clause
-- to prevent unintended mass data modifications.

-- BAD: DELETE without WHERE will delete ALL rows
DELETE FROM dbo.TempData;

-- BAD: UPDATE without WHERE will update ALL rows
UPDATE dbo.Users SET IsActive = 0;

-- BAD: Multiple statements, one without WHERE
UPDATE dbo.Orders SET Status = 'Cancelled' WHERE OrderDate < '2020-01-01';
DELETE FROM dbo.OrderItems;  -- This will delete everything!

-- GOOD: DELETE with WHERE clause
DELETE FROM dbo.TempData
WHERE CreatedDate < DATEADD(day, -30, GETDATE());

-- GOOD: UPDATE with WHERE clause
UPDATE dbo.Users
SET IsActive = 0
WHERE LastLoginDate < DATEADD(year, -2, GETDATE());

-- GOOD: Conditional UPDATE
UPDATE dbo.Orders
SET Status = 'Cancelled'
WHERE OrderDate < '2020-01-01'
  AND Status = 'Pending';
