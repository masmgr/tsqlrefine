-- avoid-or-on-different-columns: Avoid OR conditions on different columns in predicates

-- Bad: OR on different columns prevents index usage
SELECT *
FROM Users
WHERE first_name = @name OR last_name = @name;

-- Bad: Different columns in JOIN ON
SELECT *
FROM Orders o
JOIN Customers c ON o.billing_id = c.id OR o.shipping_id = c.id;

-- Good: Rewrite as UNION ALL
SELECT * FROM Users WHERE first_name = @name
UNION ALL
SELECT * FROM Users WHERE last_name = @name AND first_name <> @name;

-- Good: Same column OR is fine (index can be used)
SELECT *
FROM Users
WHERE status = 'active' OR status = 'pending';

-- Good: Use IN for same column
SELECT *
FROM Users
WHERE status IN ('active', 'pending');
