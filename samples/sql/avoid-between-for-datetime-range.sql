-- avoid-between-for-datetime-range rule examples
-- Detects BETWEEN for datetime ranges (boundary issues with time components)

-- BAD: BETWEEN with column name containing "time"
SELECT * FROM dbo.Orders WHERE CreatedTime BETWEEN @from AND @to;

-- BAD: BETWEEN with datetime function
SELECT * FROM dbo.Orders WHERE OrderDate BETWEEN GETDATE() - 7 AND GETDATE();

-- BAD: BETWEEN with CAST to datetime
SELECT * FROM dbo.Orders WHERE CAST(OrderDate AS DATETIME) BETWEEN @from AND @to;

-- BAD: BETWEEN with CONVERT to smalldatetime
SELECT * FROM dbo.Orders WHERE CONVERT(SMALLDATETIME, OrderDate) BETWEEN @from AND @to;

-- GOOD: Use >= and < pattern instead
SELECT * FROM dbo.Orders WHERE CreatedTime >= @from AND CreatedTime < @to;

-- GOOD: BETWEEN with non-datetime column (no "time" in name)
SELECT * FROM dbo.Products WHERE Price BETWEEN 10 AND 100;

-- GOOD: BETWEEN with date-only column (no "time" in name)
SELECT * FROM dbo.Orders WHERE OrderDate BETWEEN @from AND @to;
