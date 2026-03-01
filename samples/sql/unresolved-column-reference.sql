-- unresolved-column-reference rule examples
-- Detects references to columns that do not exist in the schema snapshot
-- Note: Requires --schema option with a schema snapshot file

-- BAD: Column does not exist (qualified)
SELECT u.NonExistentColumn FROM dbo.Users AS u;

-- BAD: Column does not exist (unqualified)
SELECT MissingCol FROM dbo.Users;

-- BAD: Ambiguous column reference (exists in multiple tables)
SELECT Id
FROM dbo.Users AS u
INNER JOIN dbo.Orders AS o ON u.Id = o.Id;

-- GOOD: Qualified column exists
SELECT u.Id, u.Name FROM dbo.Users AS u;

-- GOOD: Unqualified column is unique across tables
SELECT Total
FROM dbo.Users AS u
INNER JOIN dbo.Orders AS o ON u.Id = o.UserId;

-- GOOD: Wildcard is not validated
SELECT * FROM dbo.Users;

-- GOOD: Temp table columns are skipped
SELECT t.AnyColumn FROM #Temp AS t;

-- GOOD: Derived table columns are skipped
SELECT d.Col FROM (SELECT 1 AS Col) AS d;
