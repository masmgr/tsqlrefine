CREATE PROCEDURE dbo.KnownProcedure
AS
SELECT 1;
GO

-- Reported when the collected catalog is authoritative.
EXEC dbo.MissingProcedure;
