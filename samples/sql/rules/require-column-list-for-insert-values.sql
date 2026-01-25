-- require-column-list-for-insert-values rule examples
-- This rule requires INSERT VALUES statements to explicitly specify column list
-- to avoid errors when table schema changes (new columns added, column order changed).

-- BAD: INSERT without column list - brittle if table schema changes
INSERT INTO dbo.Users
VALUES (1, 'johndoe', 'john@example.com', '2024-01-15', 1);

-- BAD: Multiple rows without column list
INSERT INTO dbo.Products
VALUES
    (101, 'Widget', 'Electronics', 29.99, 100),
    (102, 'Gadget', 'Electronics', 49.99, 50);

-- BAD: INSERT into temp table without column list
CREATE TABLE #TempOrders (OrderId INT, CustomerId INT, OrderDate DATE);
INSERT INTO #TempOrders
VALUES (1001, 501, '2024-01-15');

-- GOOD: Explicitly specify column list
INSERT INTO dbo.Users (UserId, UserName, Email, CreatedDate, IsActive)
VALUES (1, 'johndoe', 'john@example.com', '2024-01-15', 1);

-- GOOD: Multiple rows with column list
INSERT INTO dbo.Products (ProductId, ProductName, Category, Price, StockQuantity)
VALUES
    (101, 'Widget', 'Electronics', 29.99, 100),
    (102, 'Gadget', 'Electronics', 49.99, 50),
    (103, 'Doohickey', 'Hardware', 19.99, 200);

-- GOOD: Partial column list (for tables with defaults/nullable columns)
INSERT INTO dbo.Users (UserName, Email)
VALUES ('janedoe', 'jane@example.com');

-- GOOD: Column list with temp table
CREATE TABLE #TempOrders (OrderId INT, CustomerId INT, OrderDate DATE);
INSERT INTO #TempOrders (OrderId, CustomerId, OrderDate)
VALUES (1001, 501, '2024-01-15');
