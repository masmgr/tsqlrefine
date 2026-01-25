-- avoid-nolock rule examples
-- This rule detects usage of NOLOCK hint or READ UNCOMMITTED isolation level
-- which can lead to dirty reads, missing rows, and duplicate rows.

-- BAD: Using NOLOCK hint
SELECT OrderId, CustomerName, TotalAmount
FROM dbo.Orders WITH (NOLOCK)
WHERE OrderDate >= '2024-01-01';

-- BAD: NOLOCK in JOIN
SELECT o.OrderId, c.CustomerName
FROM dbo.Orders o WITH (NOLOCK)
INNER JOIN dbo.Customers c WITH (NOLOCK) ON o.CustomerId = c.CustomerId;

-- BAD: READ UNCOMMITTED isolation level
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SELECT * FROM dbo.Users;

-- BAD: READUNCOMMITTED (alternative syntax)
SELECT ProductId, ProductName
FROM dbo.Products WITH (READUNCOMMITTED);

-- GOOD: No hint (uses default isolation level)
SELECT OrderId, CustomerName, TotalAmount
FROM dbo.Orders
WHERE OrderDate >= '2024-01-01';

-- GOOD: Use READ COMMITTED SNAPSHOT for better concurrency without dirty reads
-- (Note: This must be configured at database level)
SELECT o.OrderId, c.CustomerName
FROM dbo.Orders o
INNER JOIN dbo.Customers c ON o.CustomerId = c.CustomerId;

-- GOOD: Use SNAPSHOT isolation for consistency
SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
SELECT * FROM dbo.Users;

-- GOOD: Use appropriate hints like READPAST if needed
SELECT OrderId
FROM dbo.Orders WITH (READPAST)
WHERE Status = 'Pending';
