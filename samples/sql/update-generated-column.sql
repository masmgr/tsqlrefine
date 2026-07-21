-- Requires a schema snapshot where dbo.Users.Id is an identity column.
UPDATE dbo.Users SET Id = 2 WHERE Id = 1;
