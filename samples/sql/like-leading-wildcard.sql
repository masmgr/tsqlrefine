-- like-leading-wildcard: Detects LIKE patterns with leading wildcards that prevent index usage

-- Bad: Leading % wildcard prevents index usage
SELECT * FROM dbo.Users WHERE Name LIKE '%smith';
SELECT * FROM dbo.Users WHERE Email LIKE '%@gmail.com';
SELECT * FROM dbo.Users WHERE Name LIKE '%smith%';

-- Bad: Leading _ wildcard prevents index usage
SELECT * FROM dbo.Users WHERE Code LIKE '_BC';

-- Bad: Leading bracket wildcard
SELECT * FROM dbo.Users WHERE Code LIKE '[A-Z]BC';

-- Good: Trailing wildcard can use indexes
SELECT * FROM dbo.Users WHERE Name LIKE 'smith%';

-- Good: No wildcard
SELECT * FROM dbo.Users WHERE Name LIKE 'smith';

-- Good: Wildcard not at start
SELECT * FROM dbo.Users WHERE Name LIKE 'sm%th';
