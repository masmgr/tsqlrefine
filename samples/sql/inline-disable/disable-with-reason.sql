-- Disable a specific rule with a reason
/* tsqlrefine-disable avoid-select-star: legacy view depends on column order */
SELECT * FROM LegacyView;
/* tsqlrefine-enable avoid-select-star */

-- Disable all rules with a reason
/* tsqlrefine-disable: auto-generated migration script */
UPDATE Users SET Status = 1;
DELETE FROM TempData;
/* tsqlrefine-enable */

-- This violation is reported (outside disabled region)
SELECT * FROM Orders;
