-- unresolved-table-reference rule examples
-- Detects references to tables or views that do not exist in the schema snapshot
-- Note: Requires --schema option with a schema snapshot file

-- BAD: Table does not exist in schema
SELECT * FROM dbo.NonExistentTable;

-- BAD: Missing table in JOIN
SELECT u.Id
FROM dbo.Users AS u
INNER JOIN dbo.MissingTable AS m ON u.Id = m.UserId;

-- GOOD: Table exists in schema
SELECT * FROM dbo.Users;

-- GOOD: Temp tables are skipped
SELECT * FROM #TempTable;

-- GOOD: Table variables are skipped
SELECT * FROM @TableVar;

-- GOOD: System schemas are skipped
SELECT * FROM sys.objects;
SELECT * FROM INFORMATION_SCHEMA.TABLES;
