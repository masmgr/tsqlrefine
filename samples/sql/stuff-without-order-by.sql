-- stuff-without-order-by: Detects STUFF with FOR XML PATH that lacks ORDER BY

-- BAD: STUFF with FOR XML PATH but no ORDER BY - results may vary between executions
SELECT STUFF((SELECT ',' + name FROM users FOR XML PATH('')), 1, 1, '') AS names;

-- BAD: Correlated subquery without ORDER BY
SELECT
    p.id,
    STUFF((
        SELECT ', ' + c.category_name
        FROM categories c
        WHERE c.product_id = p.id
        FOR XML PATH('')
    ), 1, 2, '') AS categories
FROM products p;

-- GOOD: With ORDER BY - deterministic results
SELECT STUFF((SELECT ',' + name FROM users ORDER BY name FOR XML PATH('')), 1, 1, '') AS names;

-- GOOD: Correlated subquery with ORDER BY
SELECT
    p.id,
    STUFF((
        SELECT ', ' + c.category_name
        FROM categories c
        WHERE c.product_id = p.id
        ORDER BY c.category_name
        FOR XML PATH('')
    ), 1, 2, '') AS categories
FROM products p;

-- OK: STUFF without FOR XML PATH (normal string manipulation)
SELECT STUFF('Hello World', 1, 5, 'Goodbye');

-- OK: FOR XML PATH without STUFF
SELECT name FROM users FOR XML PATH('');
