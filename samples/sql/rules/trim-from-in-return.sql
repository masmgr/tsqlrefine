-- trim-from-in-return rule examples
-- This rule detects TRIM with a FROM clause inside RETURN statements.
-- TRIM('x' FROM ...) fails to parse when used directly in RETURN due to a known
-- ScriptDOM bug (error 46010). The same syntax parses correctly in SELECT and SET.

-- BAD: TRIM with FROM clause directly in RETURN (causes parse error)
CREATE FUNCTION dbo.TrimChars(@str NVARCHAR(MAX))
RETURNS NVARCHAR(MAX)
AS
BEGIN
    RETURN TRIM('x' FROM @str);  -- ScriptDOM cannot parse this
END;

-- BAD: TRIM with LEADING in RETURN
CREATE FUNCTION dbo.TrimLeading(@str NVARCHAR(MAX))
RETURNS NVARCHAR(MAX)
AS
BEGIN
    RETURN TRIM(LEADING 'x' FROM @str);  -- ScriptDOM cannot parse this
END;

-- BAD: TRIM with TRAILING in RETURN
CREATE FUNCTION dbo.TrimTrailing(@str NVARCHAR(MAX))
RETURNS NVARCHAR(MAX)
AS
BEGIN
    RETURN TRIM(TRAILING 'x' FROM @str);  -- ScriptDOM cannot parse this
END;

-- BAD: TRIM with BOTH in RETURN
CREATE FUNCTION dbo.TrimBoth(@str NVARCHAR(MAX))
RETURNS NVARCHAR(MAX)
AS
BEGIN
    RETURN TRIM(BOTH 'x' FROM @str);  -- ScriptDOM cannot parse this
END;

-- GOOD: Use a variable as an intermediate step (workaround)
CREATE FUNCTION dbo.TrimCharsFixed(@str NVARCHAR(MAX))
RETURNS NVARCHAR(MAX)
AS
BEGIN
    DECLARE @result NVARCHAR(MAX) = TRIM('x' FROM @str);
    RETURN @result;
END;

-- GOOD: TRIM without FROM clause parses fine in RETURN
CREATE FUNCTION dbo.TrimSpaces(@str NVARCHAR(MAX))
RETURNS NVARCHAR(MAX)
AS
BEGIN
    RETURN TRIM(@str);
END;

-- GOOD: TRIM with FROM clause parses fine in SELECT
SELECT TRIM('x' FROM col) FROM dbo.MyTable;

-- GOOD: TRIM with FROM clause parses fine in SET
DECLARE @result NVARCHAR(MAX);
SET @result = TRIM('x' FROM @str);
