-- avoid-set-rowcount rule examples
-- Detects SET ROWCOUNT n (where n > 0) which is deprecated

-- BAD: SET ROWCOUNT with positive integer
SET ROWCOUNT 100;
SELECT * FROM dbo.Users;

-- BAD: SET ROWCOUNT with variable
DECLARE @rows INT = 50;
SET ROWCOUNT @rows;
DELETE FROM dbo.OldRecords WHERE Status = 'Archived';

-- GOOD: SET ROWCOUNT 0 (resets/disables)
SET ROWCOUNT 0;

-- GOOD: Use TOP instead
SELECT TOP 100 * FROM dbo.Users;

-- GOOD: Use TOP in DML
DELETE TOP (1000) FROM dbo.OldRecords WHERE Status = 'Archived';
