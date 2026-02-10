-- duplicate-insert-column: Detects duplicate column names in INSERT column lists

-- Bad: Column 'id' specified twice
INSERT INTO dbo.Users (id, name, id)
VALUES (1, 'John', 2);

-- Bad: Case-insensitive duplicate
INSERT INTO dbo.Products (Name, Price, NAME)
VALUES ('Widget', 9.99, 'Gadget');

-- Bad: Duplicate in INSERT...SELECT
INSERT INTO dbo.Orders (order_id, customer_id, order_id)
SELECT id, cust_id, id FROM staging.Orders;

-- Good: All columns are unique
INSERT INTO dbo.Users (id, name, email)
VALUES (1, 'John', 'john@example.com');

-- Good: No column list (checked by require-column-list-for-insert-values instead)
INSERT INTO dbo.Users
VALUES (1, 'John', 'john@example.com');
