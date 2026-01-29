-- uncommitted-transaction: BEGIN TRANSACTION requires corresponding COMMIT TRANSACTION
-- Category: Reliability
-- Severity: Warning

-- ❌ BAD: BEGIN TRANSACTION without COMMIT or ROLLBACK
BEGIN TRANSACTION;
UPDATE Users SET LastLogin = GETDATE() WHERE Id = 1;
-- Missing COMMIT or ROLLBACK - transaction left open

-- ❌ BAD: Multiple BEGIN TRANSACTION, only one committed
BEGIN TRANSACTION;
UPDATE Orders SET Status = 'Processed' WHERE OrderId = 100;

BEGIN TRANSACTION;
UPDATE OrderItems SET Shipped = 1 WHERE OrderId = 100;
COMMIT TRANSACTION; -- Only commits the second transaction

-- ✅ GOOD: BEGIN TRANSACTION with COMMIT
BEGIN TRANSACTION;
UPDATE Users SET LastLogin = GETDATE() WHERE Id = 1;
COMMIT TRANSACTION;

-- ✅ GOOD: BEGIN TRANSACTION with ROLLBACK
BEGIN TRANSACTION;
DELETE FROM TempData WHERE CreatedDate < DATEADD(DAY, -7, GETDATE());
ROLLBACK TRANSACTION;

-- ✅ GOOD: Transaction with TRY/CATCH
BEGIN TRY
    BEGIN TRANSACTION;
    UPDATE Accounts SET Balance = Balance - 100 WHERE AccountId = 1;
    UPDATE Accounts SET Balance = Balance + 100 WHERE AccountId = 2;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH

-- ✅ GOOD: Nested transactions with commits
BEGIN TRANSACTION;
    UPDATE ParentTable SET UpdatedDate = GETDATE() WHERE Id = 1;

    BEGIN TRANSACTION;
        UPDATE ChildTable SET ProcessedDate = GETDATE() WHERE ParentId = 1;
    COMMIT TRANSACTION;

COMMIT TRANSACTION;
