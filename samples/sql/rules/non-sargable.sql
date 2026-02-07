-- non-sargable rule examples
-- This rule detects functions applied to columns in predicates

-- BAD: String function on column in WHERE
SELECT * FROM users WHERE LTRIM(username) = 'admin';

-- BAD: UPPER on column prevents index usage
SELECT * FROM users WHERE UPPER(username) = 'ADMIN';

-- BAD: LOWER on column prevents index usage
SELECT * FROM users WHERE LOWER(username) = 'admin';

-- BAD: Date function on column
SELECT * FROM orders WHERE YEAR(order_date) = 2023;

-- BAD: Multiple functions in WHERE
SELECT * FROM orders
WHERE YEAR(order_date) = 2023
  AND MONTH(order_date) = 12;

-- BAD: SUBSTRING in JOIN condition
SELECT *
FROM users u
INNER JOIN profiles p ON SUBSTRING(u.username, 1, 5) = p.code;

-- BAD: Nested functions on column
SELECT * FROM users WHERE LTRIM(RTRIM(username)) = 'admin';

-- BAD: Function in HAVING clause
SELECT COUNT(*)
FROM users
GROUP BY department
HAVING LTRIM(department) = 'Sales';

-- BAD: Scalar UDF wrapping a column
SELECT * FROM orders WHERE dbo.FormatDate(order_date) = '2023-01-01';

-- BAD: Scalar UDF in JOIN condition
SELECT *
FROM orders o
INNER JOIN customers c ON dbo.NormalizeCode(o.customer_code) = c.code;

-- GOOD: Direct column comparison
SELECT * FROM users WHERE username = 'admin';

-- GOOD: Case-insensitive search with collation
SELECT * FROM users WHERE username = 'ADMIN' COLLATE SQL_Latin1_General_CP1_CI_AS;

-- GOOD: Date range comparison
SELECT * FROM orders
WHERE order_date >= '2023-01-01' AND order_date < '2024-01-01';

-- GOOD: Function on literal value (not on column)
SELECT * FROM users WHERE username = LTRIM('  admin  ');

-- GOOD: Function in SELECT list (not in predicate)
SELECT LTRIM(username) FROM users;

-- GOOD: CAST/CONVERT (handled by avoid-implicit-conversion-in-predicate)
SELECT * FROM users WHERE CAST(user_id AS VARCHAR) = '123';

-- GOOD: DATEADD with constants only
SELECT [Name]
FROM Foo
WHERE Foo.DateCreated BETWEEN DATEADD(MONTH, DATEDIFF(MONTH, 0, GETDATE()), 0)
  AND DATEADD(DAY, 1, EOMONTH(DATEADD(MONTH, 0, GETDATE())));
