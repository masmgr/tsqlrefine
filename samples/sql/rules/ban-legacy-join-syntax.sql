-- ban-legacy-join-syntax rule examples
-- This rule detects legacy outer join syntax (*=, =*) which is deprecated since SQL Server 2000

-- BAD: Legacy left outer join syntax
SELECT o.OrderId, c.CustomerName
FROM Orders o, Customers c
WHERE o.CustomerId *= c.CustomerId;

-- BAD: Legacy right outer join syntax
SELECT p.ProductName, c.CategoryName
FROM Products p, Categories c
WHERE p.CategoryId =* c.CategoryId;

-- BAD: Multiple legacy joins
SELECT o.OrderId, c.CustomerName, p.ProductName
FROM Orders o, Customers c, Products p
WHERE o.CustomerId *= c.CustomerId
  AND o.ProductId *= p.ProductId;

-- GOOD: Modern ANSI left outer join
SELECT o.OrderId, c.CustomerName
FROM Orders o
LEFT JOIN Customers c ON o.CustomerId = c.CustomerId;

-- GOOD: Modern ANSI right outer join
SELECT p.ProductName, c.CategoryName
FROM Products p
RIGHT JOIN Categories c ON p.CategoryId = c.CategoryId;

-- GOOD: Multiple modern joins
SELECT o.OrderId, c.CustomerName, p.ProductName
FROM Orders o
LEFT JOIN Customers c ON o.CustomerId = c.CustomerId
LEFT JOIN Products p ON o.ProductId = p.ProductId;

-- GOOD: Mixed join types with modern syntax
SELECT o.OrderId, c.CustomerName, p.ProductName
FROM Orders o
INNER JOIN Customers c ON o.CustomerId = c.CustomerId
LEFT JOIN Products p ON o.ProductId = p.ProductId;
