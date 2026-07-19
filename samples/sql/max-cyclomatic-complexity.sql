CREATE PROCEDURE dbo.ComplexReport @value int
AS
BEGIN
    IF @value > 0 SELECT 1;
    IF @value > 1 SELECT 2;
    IF @value > 2 SELECT 3;
END;
