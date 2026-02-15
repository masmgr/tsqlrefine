-- avoid-select-distinct rule examples
-- This rule flags SELECT DISTINCT usage which often masks JOIN bugs

-- BAD: DISTINCT hiding 1:N relationship
SELECT DISTINCT c.CustomerName, c.City
FROM Customers c
JOIN Orders o ON c.Id = o.CustomerId;

-- BAD: DISTINCT as band-aid for cartesian product
SELECT DISTINCT p.ProductName
FROM Products p
CROSS JOIN Categories cat
WHERE cat.CategoryName = 'Electronics';

-- BAD: DISTINCT in subquery
SELECT *
FROM Customers c
WHERE c.CustomerId IN (
    SELECT DISTINCT o.CustomerId
    FROM Orders o
    JOIN OrderDetails od ON o.OrderId = od.OrderId
);

-- BAD: DISTINCT with multiple columns
SELECT DISTINCT o.OrderId, c.CustomerName, o.OrderDate
FROM Orders o
JOIN Customers c ON o.CustomerId = c.CustomerId
JOIN OrderDetails od ON o.OrderId = od.OrderId;

-- GOOD: Fix JOIN logic with aggregation
SELECT c.CustomerName, c.City, COUNT(*) as OrderCount
FROM Customers c
JOIN Orders o ON c.Id = o.CustomerId
GROUP BY c.CustomerName, c.City;

-- GOOD: Use EXISTS instead of JOIN when you don't need related data
SELECT c.CustomerName, c.City
FROM Customers c
WHERE EXISTS (SELECT 1 FROM Orders o WHERE o.CustomerId = c.Id);

-- GOOD: Proper JOIN without duplicates
SELECT p.ProductName
FROM Products p
JOIN Categories cat ON p.CategoryId = cat.CategoryId
WHERE cat.CategoryName = 'Electronics';

-- GOOD: Legitimate use - unique value list
SELECT DISTINCT Country
FROM Customers
ORDER BY Country;

-- GOOD: COUNT(DISTINCT ...) is allowed (different construct)
SELECT c.City, COUNT(DISTINCT c.CustomerId) as UniqueCustomers
FROM Customers c
GROUP BY c.City;

-- GOOD: UNION (has implicit DISTINCT, but appropriate for set operations)
SELECT CustomerName FROM Customers WHERE City = 'Tokyo'
UNION
SELECT CustomerName FROM Customers WHERE City = 'Osaka';
