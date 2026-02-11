-- require-parameterized-sp-executesql rule examples
-- Detects sp_executesql without proper parameterization

-- BAD: No parameter definitions
EXEC sp_executesql N'SELECT * FROM dbo.Users WHERE Id = 1';

-- BAD: Variable without parameter definitions
EXEC sp_executesql @sql;

-- GOOD: Properly parameterized
EXEC sp_executesql
    N'SELECT * FROM dbo.Users WHERE Id = @id',
    N'@id INT',
    @id = 1;

-- GOOD: Multiple parameters
EXEC sp_executesql
    N'SELECT * FROM dbo.Users WHERE Id = @id AND Name = @name',
    N'@id INT, @name NVARCHAR(100)',
    @id = 1,
    @name = N'test';
