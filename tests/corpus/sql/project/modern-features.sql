DECLARE @payload nvarchar(max) = N'{"orders":[{"id":1,"amount":12.50}]}';

SELECT j.OrderId, j.Amount
FROM OPENJSON(@payload, '$.orders')
WITH
(
    OrderId int '$.id',
    Amount decimal(18, 2) '$.amount'
) AS j;

SELECT STRING_AGG(CONVERT(nvarchar(max), c.DisplayName), N',')
       WITHIN GROUP (ORDER BY c.DisplayName)
FROM dbo.Customer AS c;
