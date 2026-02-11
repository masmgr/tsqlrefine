-- avoid-scalar-udf-in-query: Avoid user-defined scalar function calls in queries

-- Bad: Scalar UDF in SELECT list
SELECT dbo.FormatName(first_name, last_name) AS full_name
FROM Employees;

-- Bad: Scalar UDF in WHERE clause
SELECT *
FROM Orders
WHERE dbo.CalculateDiscount(order_id) > 100;

-- Bad: Scalar UDF in JOIN ON
SELECT *
FROM Orders o
JOIN Customers c ON dbo.NormalizeCode(o.customer_code) = c.code;

-- Good: Inline the logic
SELECT first_name + ' ' + last_name AS full_name
FROM Employees;

-- Good: Use inline table-valued function with CROSS APPLY
SELECT o.*, d.discount_amount
FROM Orders o
CROSS APPLY dbo.CalculateDiscountTvf(o.order_id) d;

-- Good: Built-in functions are fine
SELECT UPPER(name), GETDATE()
FROM Products;
