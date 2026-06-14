-- Rule: len-for-emptiness-check
-- Warns when LEN() is compared against zero for emptiness checks.
-- LEN() ignores trailing spaces, so whitespace-only values (including full-width spaces)
-- slip through an emptiness check. Use DATALENGTH() to detect them reliably.

-- BAD: emptiness check via LEN() = 0
SELECT * FROM dbo.Products WHERE LEN(Name) = 0;

-- BAD: non-empty check via LEN() > 0
SELECT * FROM dbo.Products WHERE LEN(Name) > 0;

-- BAD: literal on the left-hand side
SELECT * FROM dbo.Products WHERE 0 = LEN(Name);

-- GOOD: DATALENGTH() reliably detects whitespace-only values
SELECT * FROM dbo.Products WHERE DATALENGTH(Name) = 0;

-- GOOD: comparing lengths of two columns is not an emptiness/length-literal check
SELECT * FROM dbo.Products WHERE LEN(Name) = LEN(Code);

-- GOOD: character-count checks are not emptiness checks
SELECT * FROM dbo.Products WHERE LEN(Code) < 5;
SELECT * FROM dbo.Products WHERE LEN(Code) <= 5;

-- GOOD: direct string comparison
SELECT * FROM dbo.Products WHERE Name = '';
