-- disallow-order-by-ordinal rule examples
-- Forbids ORDER BY with ordinal positions which break when columns are reordered

-- BAD: Ordinal positions
SELECT id, name, email FROM dbo.Users ORDER BY 1, 2;

-- BAD: Mixed ordinal and name
SELECT id, name FROM dbo.Users ORDER BY 1, name;

-- BAD: Ordinal with direction
SELECT id, name FROM dbo.Users ORDER BY 1 DESC;

-- GOOD: Explicit column names
SELECT id, name, email FROM dbo.Users ORDER BY id, name;

-- GOOD: Column expression
SELECT id, name FROM dbo.Users ORDER BY LEN(name);
