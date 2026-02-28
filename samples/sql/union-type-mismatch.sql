-- union-type-mismatch: Detects UNION with obviously different column types

-- Bad: Numeric vs String
SELECT 1 AS Id
UNION ALL
SELECT 'text' AS Id;

-- Bad: Multiple column mismatches
SELECT 1, 'hello'
UNION ALL
SELECT 'a', 2;

-- Good: Same types
SELECT 1 AS Id
UNION ALL
SELECT 2 AS Id;

-- Good: Column references (types not determinable without schema)
SELECT Name FROM Users
UNION ALL
SELECT Title FROM Products;

-- Good: NULL is compatible with any type
SELECT 1 AS Id
UNION ALL
SELECT NULL AS Id;

-- Schema-aware: Bad - column ref type mismatch (detected with schema snapshot)
SELECT Id FROM dbo.Users        -- int
UNION ALL
SELECT Title FROM dbo.Products; -- nvarchar

-- Schema-aware: Good - same type category across tables
SELECT Id FROM dbo.Users        -- int
UNION ALL
SELECT Id FROM dbo.Products;    -- int
