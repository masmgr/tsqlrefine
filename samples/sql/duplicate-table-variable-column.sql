-- Bad: duplicate column in table variable
DECLARE @t TABLE (id INT, name VARCHAR(50), id INT);

-- Bad: case-insensitive duplicate
DECLARE @t2 TABLE (Col1 INT, COL1 VARCHAR(10));

-- Good: unique columns
DECLARE @t3 TABLE (id INT, name VARCHAR(50), age INT);
