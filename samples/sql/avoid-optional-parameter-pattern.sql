-- avoid-optional-parameter-pattern rule examples
-- Detects patterns that prevent index usage and cause plan instability

-- BAD: @p IS NULL OR col = @p pattern
SELECT * FROM dbo.Users
WHERE (@Name IS NULL OR Name = @Name);

-- BAD: Reversed order
SELECT * FROM dbo.Users
WHERE (Name = @Name OR @Name IS NULL);

-- BAD: col = ISNULL(@p, col) pattern
SELECT * FROM dbo.Users
WHERE CustomerId = ISNULL(@CustId, CustomerId);

-- BAD: Multiple optional parameters
SELECT * FROM dbo.Users
WHERE (@Name IS NULL OR Name = @Name)
  AND (@Status IS NULL OR Status = @Status);

-- GOOD: Use dynamic SQL with parameters
-- DECLARE @sql NVARCHAR(MAX) = N'SELECT * FROM dbo.Users WHERE 1=1';
-- IF @Name IS NOT NULL SET @sql += N' AND Name = @Name';
-- EXEC sp_executesql @sql, N'@Name NVARCHAR(100)', @Name = @Name;

-- GOOD: Use OPTION (RECOMPILE)
SELECT * FROM dbo.Users
WHERE (@Name IS NULL OR Name = @Name)
OPTION (RECOMPILE);

-- GOOD: Simple equality (not optional pattern)
SELECT * FROM dbo.Users
WHERE Name = @Name;
