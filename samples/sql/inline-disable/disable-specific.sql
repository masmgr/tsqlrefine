/* tsqlrefine-disable avoid-select-star */

-- Example: Disable specific rule for entire script
-- Only 'avoid-select-star' violations will be suppressed

-- This SELECT * is suppressed (avoid-select-star is disabled)
SELECT * FROM Users;

-- This UPDATE without WHERE still triggers 'dml-without-where'
UPDATE Users SET Status = 1;

-- Another SELECT * - also suppressed
SELECT * FROM Orders;
