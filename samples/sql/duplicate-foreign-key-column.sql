-- duplicate-foreign-key-column: Detects duplicate columns within a FOREIGN KEY constraint

-- Bad: Column 'a' appears twice in FOREIGN KEY
CREATE TABLE dbo.Example1 (
    a INT,
    b INT,
    FOREIGN KEY (a, b, a) REFERENCES dbo.Other (x, y, z)
);

-- Bad: Named constraint with duplicate
CREATE TABLE dbo.Example2 (
    col1 INT,
    col2 INT,
    CONSTRAINT FK_Example2 FOREIGN KEY (col1, col1) REFERENCES dbo.Other (x, y)
);

-- Good: All columns are unique
CREATE TABLE dbo.Example3 (
    a INT,
    b INT,
    FOREIGN KEY (a, b) REFERENCES dbo.Other (x, y)
);
