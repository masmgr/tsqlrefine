-- delete-column-not-in-table rule examples
-- Detects DELETE statements whose WHERE clause references columns not found in the target table
-- Note: Requires --schema option with a schema snapshot file

-- BAD: Column does not exist in target table
DELETE FROM dbo.Users WHERE BadCol = 1;

-- BAD: Qualified column does not exist
DELETE u FROM dbo.Users AS u WHERE u.NonExistent = 1;

-- BAD: Column does not exist in joined table
DELETE u
FROM dbo.Users AS u
INNER JOIN dbo.Orders AS o ON u.Id = o.UserId
WHERE o.BadCol = 1;

-- GOOD: Valid column reference
DELETE FROM dbo.Users WHERE Id = 1;

-- GOOD: Valid multi-table DELETE
DELETE u
FROM dbo.Users AS u
INNER JOIN dbo.Orders AS o ON u.Id = o.UserId
WHERE o.Total > 100;

-- GOOD: DELETE without WHERE
DELETE FROM dbo.Users;

-- GOOD: Temp table columns are skipped
DELETE FROM #Temp WHERE AnyColumn = 1;
