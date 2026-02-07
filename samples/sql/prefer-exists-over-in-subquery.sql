-- prefer-exists-over-in-subquery: Prefer EXISTS over IN with subquery

-- Bad: IN with subquery in WHERE
SELECT * FROM Users
WHERE Id IN (SELECT UserId FROM Orders);

-- Bad: NOT IN with subquery
SELECT * FROM Users
WHERE Id NOT IN (SELECT UserId FROM BlockedUsers);

-- Good: EXISTS subquery
SELECT * FROM Users u
WHERE EXISTS (SELECT 1 FROM Orders o WHERE o.UserId = u.Id);

-- Good: NOT EXISTS subquery
SELECT * FROM Users u
WHERE NOT EXISTS (SELECT 1 FROM BlockedUsers b WHERE b.UserId = u.Id);

-- Good: IN with value list (not a subquery)
SELECT * FROM Users
WHERE Status IN ('Active', 'Pending', 'Verified');
