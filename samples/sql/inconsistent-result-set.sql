CREATE PROCEDURE dbo.GetUser
    @full bit
AS
BEGIN
    IF @full = 1
        SELECT Id, Name FROM dbo.Users;
    ELSE
        SELECT Id FROM dbo.Users;
END;
