-- normalize-inequality-operator rule examples
-- Normalizes != to <> (ISO standard inequality operator)

-- BAD: Non-ISO operator
SELECT * FROM dbo.Users WHERE status != 'active';

-- BAD: Multiple uses
SELECT * FROM dbo.Orders WHERE status != 'pending' AND total != 0;

-- GOOD: ISO standard operator
SELECT * FROM dbo.Users WHERE status <> 'active';

-- GOOD: Other operators are fine
SELECT * FROM dbo.Users WHERE id = 1;
SELECT * FROM dbo.Users WHERE total > 0;
