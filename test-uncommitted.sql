-- Test case: BEGIN without COMMIT
BEGIN TRANSACTION;
UPDATE Users SET Name = 'test' WHERE Id = 1;
