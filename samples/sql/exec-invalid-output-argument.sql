CREATE PROCEDURE dbo.FindUser @id int AS SELECT @id;
GO

DECLARE @id int = 1;
EXEC dbo.FindUser @id OUTPUT;
