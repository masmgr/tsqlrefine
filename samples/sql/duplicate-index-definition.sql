-- duplicate-index-definition: Detects multiple indexes with the same column composition

-- Bad: IX_1 and IX_2 have identical column composition
CREATE TABLE dbo.Example1 (
    a INT,
    b INT,
    INDEX IX_1 (a, b),
    INDEX IX_2 (a, b)
);

-- Bad: Index and UNIQUE constraint with same columns
CREATE TABLE dbo.Example2 (
    a INT,
    b INT,
    INDEX IX_1 (a, b),
    CONSTRAINT UQ_1 UNIQUE (a, b)
);

-- Good: Different column order
CREATE TABLE dbo.Example3 (
    a INT,
    b INT,
    INDEX IX_1 (a, b),
    INDEX IX_2 (b, a)
);

-- Good: Different sort order
CREATE TABLE dbo.Example4 (
    a INT,
    INDEX IX_1 (a ASC),
    INDEX IX_2 (a DESC)
);
