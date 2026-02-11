-- set-arithabort rule examples
-- Detects files missing SET ARITHABORT ON in the preamble

-- BAD: Missing SET ARITHABORT ON
CREATE PROCEDURE dbo.CalculateAverage AS
BEGIN
    SELECT AVG(Score) FROM dbo.Results;
END;
GO

-- GOOD: SET ARITHABORT ON before CREATE
SET ARITHABORT ON;
GO
CREATE PROCEDURE dbo.CalculateAverage AS
BEGIN
    SELECT AVG(Score) FROM dbo.Results;
END;
GO
