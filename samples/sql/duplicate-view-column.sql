-- Bad: duplicate column names in VIEW
CREATE VIEW v_bad AS
SELECT id, name, id FROM users;
GO

-- Bad: duplicate alias
CREATE VIEW v_bad2 AS
SELECT a AS col, b AS col FROM t;
GO

-- Bad: explicit column list with duplicates
CREATE VIEW v_bad3 (col1, col1) AS
SELECT a, b FROM t;
GO

-- Good: unique column names
CREATE VIEW v_good AS
SELECT id, name, email FROM users;
GO

-- Good: explicit unique column list
CREATE VIEW v_good2 (user_id, user_name) AS
SELECT id, name FROM users;
