-- avoid-not-in-with-null rule examples
-- Detects NOT IN with subquery (NULL-unsafe)

-- BAD: NOT IN with subquery - if subquery returns NULL, entire result is empty
SELECT * FROM dbo.Orders
WHERE CustomerId NOT IN (SELECT CustomerId FROM dbo.Blacklist);

-- BAD: NOT IN with subquery in DELETE
DELETE FROM dbo.Orders
WHERE CustomerId NOT IN (SELECT CustomerId FROM dbo.Customers);

-- GOOD: Use NOT EXISTS instead
SELECT * FROM dbo.Orders AS o
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Blacklist AS b WHERE b.CustomerId = o.CustomerId
);

-- GOOD: Use EXCEPT
SELECT CustomerId FROM dbo.Orders
EXCEPT
SELECT CustomerId FROM dbo.Blacklist;

-- GOOD: IN with subquery (no NULL issue)
SELECT * FROM dbo.Orders
WHERE CustomerId IN (SELECT CustomerId FROM dbo.Customers);

-- GOOD: NOT IN with value list (no subquery)
SELECT * FROM dbo.Orders
WHERE Status NOT IN ('Cancelled', 'Refunded');
