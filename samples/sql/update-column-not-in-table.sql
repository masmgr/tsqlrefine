-- update-column-not-in-table rule examples
-- Detects UPDATE statements that reference columns not found in the target table
-- Note: Requires --schema option with a schema snapshot file

-- BAD: Column does not exist in target table
UPDATE dbo.Users SET NonExistentColumn = 1 WHERE Id = 1;

-- BAD: Multiple invalid columns
UPDATE dbo.Users SET Bad1 = 1, Bad2 = 2 WHERE Id = 1;

-- GOOD: All columns exist
UPDATE dbo.Users SET Name = N'Updated', Email = N'new@example.com' WHERE Id = 1;

-- GOOD: Temp table (skipped)
UPDATE #Temp SET Anything = 1;
