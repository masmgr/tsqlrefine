-- duplicate-index-column: Detects duplicate columns within a single index, PRIMARY KEY, or UNIQUE constraint

-- Bad: Column 'a' appears twice in index
CREATE TABLE dbo.Example1 (
    a INT,
    b INT,
    INDEX IX_1 (a, b, a)
);

-- Bad: Duplicate column in PRIMARY KEY
CREATE TABLE dbo.Example2 (
    a INT,
    b INT,
    CONSTRAINT PK_Example2 PRIMARY KEY (a, b, a)
);

-- Bad: Duplicate column in UNIQUE constraint
CREATE TABLE dbo.Example3 (
    x INT,
    y INT,
    CONSTRAINT UQ_Example3 UNIQUE (x, y, x)
);

-- Good: All columns are unique within each index
CREATE TABLE dbo.Example4 (
    a INT,
    b INT,
    c INT,
    INDEX IX_1 (a, b),
    CONSTRAINT PK_Example4 PRIMARY KEY (c)
);
