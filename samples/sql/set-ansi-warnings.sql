-- set-ansi-warnings rule examples
-- Detects files missing SET ANSI_WARNINGS ON in the preamble

-- BAD: Missing SET ANSI_WARNINGS ON
CREATE PROCEDURE dbo.ProcessData AS
BEGIN
    SELECT SUM(Amount) FROM dbo.Orders;
END;
GO

-- GOOD: SET ANSI_WARNINGS ON before CREATE
SET ANSI_WARNINGS ON;
GO
CREATE PROCEDURE dbo.ProcessData AS
BEGIN
    SELECT SUM(Amount) FROM dbo.Orders;
END;
GO
