-- index-column-not-in-table rule examples
-- Detects index definitions that reference columns not found in the target table
-- Note: CREATE INDEX requires --schema option; inline indexes in CREATE TABLE are validated without schema

-- BAD: Key column does not exist (CREATE INDEX, requires schema)
CREATE INDEX IX_BadCol ON dbo.Users (BadCol);

-- BAD: INCLUDE column does not exist (CREATE INDEX, requires schema)
CREATE INDEX IX_Name ON dbo.Users (Id) INCLUDE (BadCol);

-- BAD: Inline index references column not in CREATE TABLE definition
CREATE TABLE dbo.Foo (
    Id INT,
    Name NVARCHAR(50),
    INDEX IX_Bad (NonExistent)
);

-- BAD: Inline index INCLUDE references column not in CREATE TABLE definition
CREATE TABLE dbo.Bar (
    Id INT,
    INDEX IX_Name (Id) INCLUDE (MissingCol)
);

-- GOOD: Valid CREATE INDEX
CREATE INDEX IX_Name ON dbo.Users (Name);

-- GOOD: Valid CREATE INDEX with INCLUDE
CREATE INDEX IX_Name ON dbo.Users (Id) INCLUDE (Name, Email);

-- GOOD: Valid inline index
CREATE TABLE dbo.Baz (
    Id INT,
    Name NVARCHAR(50),
    INDEX IX_Name (Name)
);

-- GOOD: Temp table indexes are skipped
CREATE INDEX IX_Name ON #Temp (Anything);
