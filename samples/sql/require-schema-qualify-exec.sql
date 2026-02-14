-- require-schema-qualify-exec rule examples
-- Requires schema qualification on EXEC procedure calls

-- BAD: No schema qualification
EXEC GetUserById @UserId = 1;

-- BAD: User-defined sp_ procedure without schema
EXEC sp_MyCustomProc;

-- GOOD: Schema qualified
EXEC dbo.GetUserById @UserId = 1;

-- GOOD: System stored procedure (allowed without schema)
EXEC sp_executesql @sql;

-- GOOD: Temp procedure (allowed)
EXEC #TempProc;

-- GOOD: Variable-based execution (allowed)
DECLARE @proc NVARCHAR(100) = N'dbo.MyProc';
EXEC @proc;
