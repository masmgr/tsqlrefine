-- avoid-float-for-decimal: Detects FLOAT/REAL types which have binary rounding issues

-- Bad: FLOAT has binary representation issues for decimal values
CREATE TABLE dbo.Products (
    Price FLOAT NOT NULL,
    Cost REAL NOT NULL
);

-- Bad: FLOAT variable
DECLARE @amount FLOAT;

-- Good: DECIMAL provides exact precision
CREATE TABLE dbo.Products (
    Price DECIMAL(18, 2) NOT NULL,
    Cost NUMERIC(10, 4) NOT NULL
);

-- Good: MONEY types for currency
CREATE TABLE dbo.Orders (
    Total MONEY NOT NULL,
    Tax SMALLMONEY NOT NULL
);
