-- set-ansi-padding rule examples
-- Detects files missing SET ANSI_PADDING ON in the preamble

-- BAD: Missing SET ANSI_PADDING ON
CREATE PROCEDURE dbo.InsertUser AS
BEGIN
    INSERT INTO dbo.Users (Name) VALUES ('John');
END;
GO

-- GOOD: SET ANSI_PADDING ON before CREATE
SET ANSI_PADDING ON;
GO
CREATE PROCEDURE dbo.InsertUser AS
BEGIN
    INSERT INTO dbo.Users (Name) VALUES ('John');
END;
GO
