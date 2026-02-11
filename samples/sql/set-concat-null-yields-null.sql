-- set-concat-null-yields-null rule examples
-- Detects files missing SET CONCAT_NULL_YIELDS_NULL ON in the preamble

-- BAD: Missing SET CONCAT_NULL_YIELDS_NULL ON
CREATE PROCEDURE dbo.BuildFullName AS
BEGIN
    SELECT FirstName + ' ' + LastName FROM dbo.Users;
END;
GO

-- GOOD: SET CONCAT_NULL_YIELDS_NULL ON before CREATE
SET CONCAT_NULL_YIELDS_NULL ON;
GO
CREATE PROCEDURE dbo.BuildFullName AS
BEGIN
    SELECT FirstName + ' ' + LastName FROM dbo.Users;
END;
GO
