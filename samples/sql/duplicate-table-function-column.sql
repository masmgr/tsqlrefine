-- Bad: inline TVF with duplicate columns
CREATE FUNCTION dbo.fn_bad_inline()
RETURNS TABLE
AS
RETURN (SELECT id, id FROM users);
GO

-- Bad: multi-statement TVF with duplicate column definitions
CREATE FUNCTION dbo.fn_bad_multi()
RETURNS @result TABLE (id INT, id INT)
AS
BEGIN
    RETURN;
END;
GO

-- Good: inline TVF with unique columns
CREATE FUNCTION dbo.fn_good_inline()
RETURNS TABLE
AS
RETURN (SELECT id, name FROM users);
GO

-- Good: multi-statement TVF with unique columns
CREATE FUNCTION dbo.fn_good_multi()
RETURNS @result TABLE (id INT, name VARCHAR(50))
AS
BEGIN
    RETURN;
END;
