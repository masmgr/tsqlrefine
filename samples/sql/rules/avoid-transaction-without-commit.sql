-- avoid-transaction-without-commit rule examples
-- This rule detects BEGIN TRANSACTION without COMMIT or ROLLBACK

-- BAD: Missing commit entirely
CREATE PROCEDURE ProcessOrder @OrderId INT
AS
BEGIN
    BEGIN TRANSACTION;

    UPDATE Orders SET Status = 'Processing' WHERE Id = @OrderId;
    UPDATE Inventory SET Quantity = Quantity - 1 WHERE ProductId = 1;

    -- Missing COMMIT or ROLLBACK!
END;
GO

-- BAD: Missing rollback on error path
CREATE PROCEDURE UpdateUser @UserId INT, @Status VARCHAR(50)
AS
BEGIN
    BEGIN TRANSACTION;

    UPDATE Users SET Status = @Status WHERE Id = @UserId;

    IF @Status = 'Active'
    BEGIN
        COMMIT TRANSACTION;
    END
    -- Missing ROLLBACK for else path!
END;
GO

-- BAD: Transaction in batch without termination
BEGIN TRANSACTION;

UPDATE Table1 SET Col = 'Value';
INSERT INTO Table2 VALUES ('Data');

-- Missing COMMIT/ROLLBACK

-- GOOD: Explicit commit
CREATE PROCEDURE ProcessOrderGood @OrderId INT
AS
BEGIN
    BEGIN TRANSACTION;

    UPDATE Orders SET Status = 'Processing' WHERE Id = @OrderId;
    UPDATE Inventory SET Quantity = Quantity - 1 WHERE ProductId = 1;

    COMMIT TRANSACTION;
END;
GO

-- GOOD: Error handling with rollback
CREATE PROCEDURE SafeTransaction @UserId INT
AS
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE Users SET Status = 'Active' WHERE Id = @UserId;
        INSERT INTO AuditLog (UserId, Action) VALUES (@UserId, 'StatusChange');

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

-- GOOD: Conditional with both paths covered
CREATE PROCEDURE ConditionalTransaction @Type VARCHAR(50)
AS
BEGIN
    BEGIN TRANSACTION;

    IF @Type = 'A'
    BEGIN
        UPDATE TableA SET Status = 'Done';
        COMMIT TRANSACTION;
    END
    ELSE
    BEGIN
        UPDATE TableB SET Status = 'Done';
        COMMIT TRANSACTION;
    END
END;
GO

-- GOOD: Transaction with validation and rollback
CREATE PROCEDURE ValidatedTransaction @Amount DECIMAL(18,2)
AS
BEGIN
    BEGIN TRANSACTION;

    UPDATE Account SET Balance = Balance - @Amount WHERE Id = @SourceId;

    IF (SELECT Balance FROM Account WHERE Id = @SourceId) < 0
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50000, 'Insufficient funds', 1;
    END

    UPDATE Account SET Balance = Balance + @Amount WHERE Id = @TargetId;

    COMMIT TRANSACTION;
END;
GO

-- GOOD: Savepoint pattern
CREATE PROCEDURE SavepointTransaction
AS
BEGIN
    BEGIN TRANSACTION;

    UPDATE MainTable SET Value = 1;

    SAVE TRANSACTION SavePoint1;

    BEGIN TRY
        UPDATE DetailTable SET Status = 'Processed';
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION SavePoint1;
    END CATCH;

    COMMIT TRANSACTION;
END;
GO
