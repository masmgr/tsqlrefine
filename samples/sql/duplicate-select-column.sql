-- Bad: duplicate column names
SELECT id, name, id FROM users;

-- Bad: duplicate aliases
SELECT a AS col, b AS col FROM t;

-- Bad: qualified columns with same base name
SELECT t.id, s.id FROM t INNER JOIN s ON t.id = s.id;

-- Good: unique column names
SELECT id, name, email FROM users;

-- Good: qualified columns with different base names
SELECT t.id, s.name FROM t INNER JOIN s ON t.id = s.id;
