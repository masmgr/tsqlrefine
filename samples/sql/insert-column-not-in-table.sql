-- insert-column-not-in-table rule examples
-- Detects INSERT statements that reference columns not found in the target table
-- Note: Requires --schema option with a schema snapshot file

-- BAD: Column does not exist in target table
INSERT INTO dbo.Users (Id, Name, NonExistentColumn) VALUES (1, N'Test', N'Bad');

-- BAD: Multiple invalid columns
INSERT INTO dbo.Users (BadCol1, BadCol2) VALUES (1, 2);

-- GOOD: All columns exist
INSERT INTO dbo.Users (Id, Name, Email) VALUES (1, N'Test', N'test@example.com');

-- GOOD: No column list (not validated)
INSERT INTO dbo.Users VALUES (1, N'Test', N'test@example.com');

-- GOOD: Temp table (skipped)
INSERT INTO #Temp (Id, Anything) VALUES (1, 2);
