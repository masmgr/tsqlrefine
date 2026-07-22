-- Function calls cannot be passed directly as EXEC procedure arguments.
EXECUTE dbo.Proc1
    @date = GETDATE(),
    @value = ABS(-1);

DECLARE @date datetime = GETDATE();
EXECUTE dbo.Proc1 @date = @date;
