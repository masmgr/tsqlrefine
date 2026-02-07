-- avoid-openrowset-opendatasource: Detects OPENROWSET and OPENDATASOURCE usage

-- Bad: OPENROWSET with bulk import
SELECT *
FROM OPENROWSET(BULK 'C:\data\file.csv',
    FORMATFILE = 'C:\data\format.fmt') AS t;

-- Bad: OPENROWSET with provider
SELECT *
FROM OPENROWSET('SQLNCLI', 'Server=remote;Trusted_Connection=yes;',
    'SELECT * FROM dbo.Users') AS t;

-- Good: Use proper table references
SELECT * FROM dbo.Users;

-- Good: Use OPENJSON for JSON data
SELECT * FROM OPENJSON(@json) AS j;
