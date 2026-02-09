-- duplicate-column-definition: Detects duplicate column names in CREATE TABLE definitions

-- Bad: Column 'id' defined twice
CREATE TABLE dbo.Users (
    id INT NOT NULL,
    name VARCHAR(50),
    id INT
);

-- Bad: Case-insensitive duplicate
CREATE TABLE dbo.Products (
    Name VARCHAR(100),
    Price DECIMAL(10, 2),
    NAME VARCHAR(200)
);

-- Good: All columns are unique
CREATE TABLE dbo.Orders (
    order_id INT NOT NULL,
    customer_id INT,
    order_date DATE
);
