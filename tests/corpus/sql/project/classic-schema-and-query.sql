CREATE TABLE dbo.Customer
(
    CustomerId int NOT NULL PRIMARY KEY,
    DisplayName nvarchar(100) NOT NULL,
    CreatedAt datetime2 NOT NULL
);
GO

CREATE TABLE dbo.SalesOrder
(
    SalesOrderId int NOT NULL PRIMARY KEY,
    CustomerId int NOT NULL,
    TotalAmount decimal(18, 2) NOT NULL,
    CONSTRAINT FK_SalesOrder_Customer FOREIGN KEY (CustomerId)
        REFERENCES dbo.Customer (CustomerId)
);
GO

WITH customer_totals AS
(
    SELECT c.CustomerId, c.DisplayName, SUM(o.TotalAmount) AS TotalAmount
    FROM dbo.Customer AS c
    INNER JOIN dbo.SalesOrder AS o ON o.CustomerId = c.CustomerId
    GROUP BY c.CustomerId, c.DisplayName
)
SELECT CustomerId, DisplayName, TotalAmount
FROM customer_totals
WHERE TotalAmount > 1000.00
ORDER BY TotalAmount DESC;
