-- Rule: multi-row-update-from
-- Warns on UPDATE...FROM with a JOIN, which can match multiple rows per target row
-- and produce a non-deterministic update. This is a schema-free syntactic check; when a
-- schema snapshot is available, update-join-cardinality-mismatch performs precise detection.

-- BAD: UPDATE...FROM with an INNER JOIN
UPDATE o SET o.Amount = oi.Quantity * 10
FROM dbo.Orders AS o
INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = o.OrderId;

-- BAD: UPDATE...FROM with a LEFT JOIN
UPDATE o SET o.Status = 'logged'
FROM dbo.Orders AS o
LEFT JOIN dbo.OrderLog AS ol ON ol.OrderId = o.OrderId;

-- BAD: multiple joins still reports once (on the first join)
UPDATE o SET o.Total = oi.Quantity * p.Price
FROM dbo.Orders AS o
INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = o.OrderId
INNER JOIN dbo.Products AS p ON p.ProductId = oi.ProductId;

-- GOOD: simple UPDATE without FROM/JOIN
UPDATE dbo.Orders SET Status = 'done' WHERE OrderId = 1;

-- GOOD: UPDATE...FROM without a JOIN (single source table)
UPDATE o SET o.Status = 'pending'
FROM dbo.Orders AS o
WHERE o.Amount IS NULL;
