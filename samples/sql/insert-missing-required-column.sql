-- Requires a schema snapshot where dbo.Users.Name is NOT NULL without a default.
INSERT dbo.Users (Email) VALUES (N'user@example.com');
