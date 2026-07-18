-- Assumes dbo.TryGet(@id int, @found bit OUTPUT).
DECLARE @found bit;
EXEC dbo.TryGet 42, @found;
