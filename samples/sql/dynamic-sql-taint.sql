CREATE PROCEDURE dbo.FindUser @userName nvarchar(100)
AS
BEGIN
    DECLARE @sql nvarchar(max) = N'SELECT * FROM dbo.Users WHERE Name = ''';
    SET @sql = @sql + @userName + N'''';
    EXEC sys.sp_executesql @sql;
END;
