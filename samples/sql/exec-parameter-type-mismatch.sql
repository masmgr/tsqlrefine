-- Assumes dbo.TakeId(@id int).
DECLARE @id bigint = 2147483648;
EXEC dbo.TakeId @id;
