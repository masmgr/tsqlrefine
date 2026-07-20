CREATE PROCEDURE dbo.ManyParameters
    @first int,
    @second int,
    @third int
AS
SELECT @first + @second + @third;
