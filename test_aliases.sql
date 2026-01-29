SELECT
    COUNT(*) AS ordercount,
    SUM(total) AS totalamount,
    u.name AS customername
FROM users u
