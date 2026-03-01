-- join-column-deviation: Detects JOINs where the column combination deviates
-- from the dominant pattern observed in the relation profile.
-- Requires: --schema and --relations-profile options.

-- Rare pattern: uses an uncommon column combination
SELECT o.OrderId, u.Name
FROM dbo.Orders AS o
INNER JOIN dbo.Users AS u ON u.Id = o.CreatedBy;

-- Very rare pattern: composite key with uncommon columns
SELECT o.OrderId, u.Name
FROM dbo.Orders AS o
INNER JOIN dbo.Users AS u ON o.Amount = u.Id AND o.CreatedBy = u.Id;

-- Unseen pattern: column combination not observed in the relation profile
SELECT o.OrderId, u.Name
FROM dbo.Orders AS o
FULL JOIN dbo.Users AS u ON u.Id = o.UserId;

-- Unknown table pair: table pair not found in the relation profile
SELECT o.OrderId, p.Name
FROM dbo.Orders AS o
INNER JOIN dbo.Products AS p ON p.Id = o.ProductId;

-- LEFT/RIGHT normalization: reversed table order still detected
SELECT u.Name, o.OrderId
FROM dbo.Users AS u
INNER JOIN dbo.Orders AS o ON o.Amount = u.Id;

-- Dominant pattern: no warning (most common pattern)
SELECT o.OrderId, u.Name
FROM dbo.Orders AS o
INNER JOIN dbo.Users AS u ON u.Id = o.UserId;
