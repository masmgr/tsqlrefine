-- avoid-catch-swallowing rule examples
-- This rule detects CATCH blocks that suppress errors without logging/rethrowing

-- BAD: Empty CATCH block
BEGIN TRY
    UPDATE Users SET Status = 'Active';
END TRY
BEGIN CATCH
    -- Nothing here - error completely swallowed!
END CATCH;

-- BAD: Only PRINT (non-persistent)
BEGIN TRY
    DELETE FROM Orders WHERE OrderId = @OrderId;
END TRY
BEGIN CATCH
    PRINT 'Error occurred';
END CATCH;

-- BAD: Only SELECT (output lost in stored procedures)
BEGIN TRY
    INSERT INTO Customers (Name, Email) VALUES (@Name, @Email);
END TRY
BEGIN CATCH
    SELECT ERROR_MESSAGE() AS ErrorMessage;
END CATCH;

-- BAD: Only setting flag without propagation
DECLARE @Success BIT = 1;
BEGIN TRY
    UPDATE Inventory SET Quantity = Quantity - 1;
END TRY
BEGIN CATCH
    SET @Success = 0;
END CATCH;

-- GOOD: Log and rethrow
BEGIN TRY
    UPDATE Users SET Status = 'Active';
END TRY
BEGIN CATCH
    INSERT INTO ErrorLog (Message, ErrorNumber, ErrorTime)
    VALUES (ERROR_MESSAGE(), ERROR_NUMBER(), GETDATE());

    THROW;
END CATCH;

-- GOOD: Just rethrow (minimal)
BEGIN TRY
    DELETE FROM Orders WHERE OrderId = @OrderId;
END TRY
BEGIN CATCH
    THROW;
END CATCH;

-- GOOD: Contextual error with rethrow
BEGIN TRY
    INSERT INTO Customers (Name, Email) VALUES (@Name, @Email);
END TRY
BEGIN CATCH
    DECLARE @ErrorMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorNum INT = ERROR_NUMBER();

    INSERT INTO ErrorLog (Context, ErrorMessage, ErrorNumber)
    VALUES ('InsertCustomer', @ErrorMsg, @ErrorNum);

    THROW;
END CATCH;

-- GOOD: Conditional error handling (handle specific, rethrow others)
BEGIN TRY
    INSERT INTO Users (Username, Email) VALUES (@Username, @Email);
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 2627  -- Duplicate key
    BEGIN
        SELECT @UserId = UserId FROM Users WHERE Username = @Username;
    END
    ELSE
    BEGIN
        THROW;
    END
END CATCH;

-- GOOD: Cleanup with rethrow
BEGIN TRY
    EXEC dbo.ProcessOrder @OrderId;
END TRY
BEGIN CATCH
    IF OBJECT_ID('tempdb..#OrderDetails') IS NOT NULL
        DROP TABLE #OrderDetails;

    THROW;
END CATCH;
