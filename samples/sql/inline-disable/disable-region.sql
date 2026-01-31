-- Example: Disable rules for a specific region only
-- Violations outside the disabled region are still reported

/* tsqlrefine-disable */

-- These statements are in the disabled region - no violations reported
SELECT * FROM Users;
UPDATE Users SET Status = 1;
DELETE FROM TempData;

/* tsqlrefine-enable */

-- This SELECT * triggers 'avoid-select-star' again (after enable)
SELECT * FROM Orders;
