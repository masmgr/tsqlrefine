/* tsqlrefine-disable avoid-select-star, dml-without-where */

-- Example: Disable multiple specific rules
-- Only the listed rules are suppressed

-- SELECT * is suppressed (avoid-select-star in list)
SELECT * FROM Users;

-- UPDATE without WHERE is suppressed (dml-without-where in list)
UPDATE Users SET Status = 1;

-- DELETE without WHERE is suppressed (dml-without-where in list)
DELETE FROM TempData;

/* tsqlrefine-enable avoid-select-star, dml-without-where */

-- These violations are reported again after enable
SELECT * FROM Orders;
UPDATE Orders SET Status = 2;
