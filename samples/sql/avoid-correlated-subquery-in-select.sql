-- avoid-correlated-subquery-in-select: Avoid correlated scalar subqueries in SELECT list

-- Bad: Correlated scalar subquery in SELECT
SELECT
    o.order_id,
    o.customer_id,
    (SELECT c.name FROM Customers c WHERE c.id = o.customer_id) AS customer_name
FROM Orders o;

-- Bad: Multiple correlated scalar subqueries
SELECT
    e.id,
    (SELECT d.name FROM Departments d WHERE d.id = e.dept_id) AS dept_name,
    (SELECT m.name FROM Employees m WHERE m.id = e.manager_id) AS manager_name
FROM Employees e;

-- Good: Use JOIN instead
SELECT o.order_id, o.customer_id, c.name AS customer_name
FROM Orders o
JOIN Customers c ON c.id = o.customer_id;

-- Good: Use CROSS APPLY for complex logic
SELECT o.order_id, ca.customer_name
FROM Orders o
CROSS APPLY (
    SELECT c.name AS customer_name
    FROM Customers c
    WHERE c.id = o.customer_id
) ca;

-- OK: Non-correlated scalar subquery (no outer reference)
SELECT
    o.order_id,
    (SELECT MAX(id) FROM Customers) AS max_customer_id
FROM Orders o;
