-- Rule: update-join-cardinality-mismatch
-- Detects UPDATE...FROM...JOIN where the join may produce multiple rows per target row.

-- BAD: OrderItems has multiple rows per OrderId (1:N)
UPDATE o SET o.Amount = oi.Quantity * 10
FROM dbo.Orders AS o
INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = o.OrderId;

-- BAD: LEFT JOIN with same 1:N issue
UPDATE o SET o.Status = 'logged'
FROM dbo.Orders AS o
LEFT JOIN dbo.OrderLog AS ol ON ol.OrderId = o.OrderId;

-- BAD: Neither side has unique join columns (M:N)
UPDATE o SET o.Status = 'has-log'
FROM dbo.Orders AS o
INNER JOIN dbo.OrderLog AS ol ON ol.Action = o.Status;

-- GOOD: Customers has unique CustomerId (PK), so N:1
UPDATE o SET o.Status = c.Name
FROM dbo.Orders AS o
INNER JOIN dbo.Customers AS c ON c.CustomerId = o.CustomerId;

-- GOOD: OrderSummary has unique OrderId (PK), so 1:1
UPDATE o SET o.Amount = s.TotalAmount
FROM dbo.Orders AS o
INNER JOIN dbo.OrderSummary AS s ON s.OrderId = o.OrderId;

-- GOOD: Simple UPDATE without JOIN
UPDATE dbo.Orders SET Status = 'done' WHERE OrderId = 1;
