-- dangerous-ddl rule examples
-- This rule detects destructive DDL operations

-- BAD: DROP DATABASE (Error severity)
DROP DATABASE ProductionDatabase;

-- BAD: DROP TABLE
DROP TABLE dbo.Users;

-- BAD: DROP TABLE IF EXISTS
DROP TABLE IF EXISTS dbo.Orders;

-- BAD: DROP VIEW
DROP VIEW dbo.CustomerSummary;

-- BAD: DROP PROCEDURE
DROP PROCEDURE dbo.ProcessOrder;

-- BAD: DROP FUNCTION
DROP FUNCTION dbo.CalculateTax;

-- BAD: TRUNCATE TABLE
TRUNCATE TABLE dbo.AuditLog;

-- BAD: ALTER TABLE DROP COLUMN
ALTER TABLE dbo.Customers
DROP COLUMN LegacyField;

-- BAD: ALTER TABLE DROP CONSTRAINT
ALTER TABLE dbo.Orders
DROP CONSTRAINT FK_Orders_Customers;

-- GOOD: Temp table drops are allowed
DROP TABLE #TempResults;
DROP TABLE ##GlobalTempData;

-- GOOD: Use DELETE instead of TRUNCATE for safety
DELETE FROM dbo.AuditLog
WHERE LogDate < DATEADD(day, -30, GETDATE());

-- GOOD: Rename instead of DROP (allows rollback)
EXEC sp_rename 'dbo.Orders', 'Orders_OLD';

-- GOOD: For schema changes, use multi-step migration
-- Step 1: Add new column
ALTER TABLE dbo.Customers ADD Email NVARCHAR(255) NULL;

-- Step 2: Migrate data (in separate transaction)
UPDATE dbo.Customers SET Email = LegacyEmailField WHERE Email IS NULL;

-- Step 3: Drop old column later (after verification)
-- ALTER TABLE dbo.Customers DROP COLUMN LegacyEmailField;
