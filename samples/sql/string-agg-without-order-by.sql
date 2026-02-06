-- STRING_AGG without ORDER BY (will trigger warning)
-- Results may be non-deterministic without explicit ordering

-- Bad: No ORDER BY specified
SELECT STRING_AGG(name, ',') AS names
FROM users;

SELECT id, STRING_AGG(tag, '; ') AS tags
FROM items
GROUP BY id;

SELECT STRING_AGG(CAST(id AS VARCHAR(10)), ',') AS id_list
FROM orders;

-- Good: ORDER BY specified with WITHIN GROUP
SELECT STRING_AGG(name, ',') WITHIN GROUP (ORDER BY name) AS names
FROM users;

SELECT id, STRING_AGG(tag, '; ') WITHIN GROUP (ORDER BY tag ASC) AS tags
FROM items
GROUP BY id;

SELECT STRING_AGG(name, ', ') WITHIN GROUP (ORDER BY created_at DESC) AS recent_names
FROM users;
