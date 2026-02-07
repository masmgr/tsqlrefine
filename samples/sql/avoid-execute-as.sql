-- avoid-execute-as: Detects EXECUTE AS usage for privilege escalation

-- Bad: Standalone EXECUTE AS USER
EXECUTE AS USER = 'dbo';
SELECT * FROM sys.databases;
REVERT;

-- Bad: EXECUTE AS LOGIN
EXECUTE AS LOGIN = 'sa';
SELECT 1;
REVERT;

-- Bad: EXECUTE AS OWNER in stored procedure
CREATE PROCEDURE dbo.MyProc
WITH EXECUTE AS OWNER
AS
BEGIN
    SELECT 1;
END;
GO

-- Bad: EXECUTE AS SELF in function
CREATE FUNCTION dbo.MyFunc()
RETURNS INT
WITH EXECUTE AS SELF
AS
BEGIN
    RETURN 1;
END;
GO

-- Good: No EXECUTE AS
CREATE PROCEDURE dbo.SafeProc
AS
BEGIN
    SELECT 1;
END;
GO

-- Good: EXECUTE AS CALLER (default, no privilege change)
CREATE PROCEDURE dbo.CallerProc
WITH EXECUTE AS CALLER
AS
BEGIN
    SELECT 1;
END;
GO
