-- avoid-dangerous-procedures: Detects usage of dangerous extended stored procedures

-- Bad: OS command execution via xp_cmdshell
EXEC xp_cmdshell 'dir C:\';

-- Bad: Registry manipulation
EXEC xp_regread @rootkey = N'HKEY_LOCAL_MACHINE', @key = N'SOFTWARE\Microsoft', @value_name = N'Version';
EXEC xp_regwrite @rootkey = N'HKEY_LOCAL_MACHINE', @key = N'SOFTWARE\MyApp', @value_name = N'Setting', @type = N'REG_SZ', @value = N'test';

-- Bad: OLE Automation procedures
DECLARE @obj INT;
EXEC sp_OACreate 'Scripting.FileSystemObject', @obj OUTPUT;
EXEC sp_OAMethod @obj, 'DeleteFile', NULL, 'C:\temp\file.txt';
EXEC sp_OADestroy @obj;

-- Good: Normal stored procedure calls
EXEC dbo.GetUsers @id = 1;
EXEC sp_executesql N'SELECT @val', N'@val INT', @val = 1;
EXEC sp_help 'dbo.Users';
