-- Bad: column 'b' is not in GROUP BY or an aggregate function
SELECT a, b FROM t GROUP BY a;

-- Bad: qualified column 't.b' not in GROUP BY
SELECT t.a, t.b FROM t GROUP BY t.a;

-- Bad: multiple non-aggregated columns
SELECT a, b, c FROM t GROUP BY a;

-- Bad: column in expression not in GROUP BY
SELECT a, b + 1 FROM t GROUP BY a;

-- Bad: column in CASE not in GROUP BY
SELECT a, CASE WHEN b > 0 THEN b ELSE 0 END FROM t GROUP BY a;

-- Good: all columns in GROUP BY
SELECT a, b FROM t GROUP BY a, b;

-- Good: column wrapped in aggregate function
SELECT a, COUNT(b) FROM t GROUP BY a;

-- Good: constant literal
SELECT a, 1 AS one FROM t GROUP BY a;

-- Good: scalar subquery
SELECT a, (SELECT MAX(c) FROM s) FROM t GROUP BY a;

-- Good: window function
SELECT a, ROW_NUMBER() OVER(ORDER BY b) FROM t GROUP BY a;
