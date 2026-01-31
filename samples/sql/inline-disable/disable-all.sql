/* tsqlrefine-disable */

-- Example: Disable all rules for entire script
-- No violations will be reported for this file

-- This SELECT * would normally trigger 'avoid-select-star'
SELECT * FROM Users;

-- This UPDATE without WHERE would normally trigger 'dml-without-where'
UPDATE Users SET Status = 1;

-- This DELETE without WHERE would normally trigger 'dml-without-where'
DELETE FROM TempData;
