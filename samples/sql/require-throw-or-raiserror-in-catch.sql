-- require-throw-or-raiserror-in-catch rule examples
-- Detects CATCH blocks without error propagation

-- BAD: Log only, no error propagation
BEGIN TRY
    INSERT INTO dbo.Orders (Id) VALUES (1);
END TRY
BEGIN CATCH
    INSERT INTO dbo.ErrorLog (Message) VALUES (ERROR_MESSAGE());
END CATCH;

-- BAD: Print only
BEGIN TRY
    SELECT 1;
END TRY
BEGIN CATCH
    PRINT ERROR_MESSAGE();
END CATCH;

-- BAD: Bare RETURN (no error code)
BEGIN TRY
    SELECT 1;
END TRY
BEGIN CATCH
    RETURN;
END CATCH;

-- GOOD: Log and rethrow
BEGIN TRY
    INSERT INTO dbo.Orders (Id) VALUES (1);
END TRY
BEGIN CATCH
    INSERT INTO dbo.ErrorLog (Message) VALUES (ERROR_MESSAGE());
    THROW;
END CATCH;

-- GOOD: RAISERROR
BEGIN TRY
    SELECT 1;
END TRY
BEGIN CATCH
    DECLARE @msg NVARCHAR(4000) = ERROR_MESSAGE();
    RAISERROR(@msg, 16, 1);
END CATCH;

-- GOOD: RETURN with error code
BEGIN TRY
    SELECT 1;
END TRY
BEGIN CATCH
    INSERT INTO dbo.ErrorLog (Message) VALUES (ERROR_MESSAGE());
    RETURN -1;
END CATCH;
