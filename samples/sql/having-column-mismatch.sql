-- Bad: column 'b' in HAVING is not in GROUP BY or an aggregate function
SELECT a FROM t GROUP BY a HAVING b > 5;

-- Bad: qualified column 't.b' not in GROUP BY
SELECT t.a FROM t GROUP BY t.a HAVING t.b > 5;

-- Bad: multiple non-aggregated columns in HAVING
SELECT a FROM t GROUP BY a HAVING b > 5 AND c < 10;

-- Bad: column in LIKE predicate not in GROUP BY
SELECT a FROM t GROUP BY a HAVING b LIKE 'x%';

-- Bad: column in IS NULL not in GROUP BY
SELECT a FROM t GROUP BY a HAVING b IS NULL;

-- Good: all HAVING columns in GROUP BY
SELECT a, b FROM t GROUP BY a, b HAVING a > 5;

-- Good: aggregate function in HAVING
SELECT a, COUNT(*) FROM t GROUP BY a HAVING COUNT(*) > 5;

-- Good: GROUP BY column used in HAVING
SELECT a FROM t GROUP BY a HAVING a > 5;

-- Good: multiple aggregates in HAVING
SELECT a, COUNT(*), AVG(b) FROM t GROUP BY a HAVING COUNT(*) > 5 AND AVG(b) > 10;

-- Good: subquery in HAVING
SELECT a FROM t GROUP BY a HAVING a > (SELECT MAX(x) FROM s);
