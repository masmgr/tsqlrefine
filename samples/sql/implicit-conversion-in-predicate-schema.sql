-- Rule: implicit-conversion-in-predicate-schema
-- Detects implicit type conversions on columns in predicates using schema type information.

-- Bad: varchar column compared with nvarchar literal (column is converted)
SELECT Email FROM dbo.Users WHERE Email = N'test@example.com';

-- Bad: varchar column compared with int literal (column is converted)
SELECT Email FROM dbo.Users WHERE Email = 1;

-- Bad: int column compared with decimal column in JOIN
SELECT u.Id
FROM dbo.Users AS u
INNER JOIN dbo.Orders AS o ON u.Id = o.Total;

-- Good: same-type comparison (no conversion)
SELECT Id FROM dbo.Users WHERE Id = 1;

-- Good: nvarchar column with nvarchar literal
SELECT Name FROM dbo.Users WHERE Name = N'Test';

-- Good: literal-side conversion only (int column vs varchar literal)
SELECT Id FROM dbo.Users WHERE Id = '1';
