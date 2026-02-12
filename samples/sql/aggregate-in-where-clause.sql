-- Bad: COUNT(*) used directly in WHERE clause
SELECT * FROM t WHERE COUNT(*) > 5;

-- Bad: SUM in WHERE comparison
SELECT * FROM t WHERE SUM(amount) > 100;

-- Bad: multiple aggregates in WHERE
SELECT * FROM t WHERE COUNT(*) > 0 AND SUM(x) > 10;

-- Bad: aggregate in BETWEEN
SELECT * FROM t WHERE MIN(x) BETWEEN 1 AND 10;

-- Bad: aggregate in IS NULL
SELECT * FROM t WHERE SUM(x) IS NULL;

-- Good: aggregate in subquery within WHERE
SELECT * FROM t WHERE x > (SELECT COUNT(*) FROM s);

-- Good: aggregate in HAVING (correct usage)
SELECT a, COUNT(*) FROM t GROUP BY a HAVING COUNT(*) > 5;

-- Good: no aggregates in WHERE
SELECT * FROM t WHERE x > 5 AND y = 'abc';

-- Good: aggregate in EXISTS subquery
SELECT * FROM t WHERE EXISTS (SELECT 1 FROM s HAVING COUNT(*) > 0);
