-- join-table-not-referenced-in-on rule examples
-- This rule detects JOIN operations where the joined table is not referenced in the ON clause

-- BAD: Joined table t2 is not referenced in ON clause
SELECT o.OrderId, o.CustomerId
FROM Orders o
INNER JOIN Customers c ON o.Status = 'Active';

-- BAD: Joined table with alias not referenced
SELECT a.col1, b.col2
FROM TableA a
LEFT JOIN TableB b ON a.col1 = 'value';

-- BAD: Self-reference on first table only
SELECT *
FROM Products p
RIGHT JOIN Categories c ON p.Price > 100;

-- BAD: Multiple joins, both missing table references
SELECT e.Name, d.Name, m.Name
FROM Employees e
JOIN Departments d ON e.Status = 1
JOIN Managers m ON d.IsActive = 1;

-- GOOD: Both tables referenced in ON clause
SELECT o.OrderId, c.CustomerName
FROM Orders o
INNER JOIN Customers c ON o.CustomerId = c.CustomerId;

-- GOOD: Joined table referenced with additional condition
SELECT p.ProductName, c.CategoryName
FROM Products p
LEFT JOIN Categories c ON p.CategoryId = c.CategoryId AND c.IsActive = 1;

-- GOOD: Multiple tables all properly referenced
SELECT e.Name, d.DeptName, m.ManagerName
FROM Employees e
JOIN Departments d ON e.DeptId = d.DeptId
JOIN Managers m ON d.ManagerId = m.ManagerId;

-- GOOD: CROSS JOIN (no ON clause expected)
SELECT a.col, b.col
FROM TableA a
CROSS JOIN TableB b;

-- GOOD: Comma join with WHERE (not a QualifiedJoin)
SELECT a.col, b.col
FROM TableA a, TableB b
WHERE a.id = b.id;

-- GOOD: FULL OUTER JOIN (not checked by this rule)
SELECT *
FROM TableA a
FULL OUTER JOIN TableB b ON a.id = 1;

-- GOOD: Joined table referenced via IS NOT NULL
SELECT *
FROM Orders o
LEFT JOIN OrderDetails od ON od.OrderId IS NOT NULL;

-- GOOD: Joined table referenced in IN clause
SELECT *
FROM Products p
JOIN Categories c ON c.CategoryId IN (1, 2, 3);
