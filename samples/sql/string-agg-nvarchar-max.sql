-- STRING_AGG first argument should be explicitly cast to NVARCHAR(MAX)
-- to avoid intermediate result truncation (8000-byte / 4000-char limit).

-- Bad: bare column reference (type inferred from input, may truncate)
SELECT STRING_AGG(name, ',') AS names
FROM users;

-- Bad: cast to a non-Unicode type
SELECT STRING_AGG(CAST(id AS VARCHAR(10)), ',') AS ids
FROM users;

-- Bad: sized nvarchar(n) still truncates
SELECT STRING_AGG(CAST(name AS NVARCHAR(100)), ',') AS names
FROM users;

-- Bad: varchar(max) is non-Unicode
SELECT STRING_AGG(CONVERT(VARCHAR(MAX), name), ',') AS names
FROM users;

-- Good: explicit CAST to NVARCHAR(MAX)
SELECT STRING_AGG(CAST(name AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY name) AS names
FROM users;

-- Good: explicit CONVERT to NVARCHAR(MAX)
SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), name), ',') WITHIN GROUP (ORDER BY name) AS names
FROM users;
