-- Test file for token categorization
DECLARE @userId INT = 1;
DECLARE @userName NVARCHAR(50);

SELECT
    u.UserId,
    u.UserName,
    COUNT(*) AS OrderCount,
    SUM(o.TotalAmount) AS TotalSpent,
    GETDATE() AS CurrentDate,
    @@ROWCOUNT AS RowCount
FROM dbo.Users u
INNER JOIN sys.Orders o ON u.UserId = o.UserId
WHERE u.IsActive = 1
    AND o.OrderDate >= DATEADD(day, -30, GETDATE())
GROUP BY u.UserId, u.UserName;

INSERT INTO staging.UserLog (UserId, LogDate, Action)
VALUES (@userId, GETDATE(), 'Login');
