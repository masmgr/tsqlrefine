-- require-save-transaction-in-nested: Require SAVE TRANSACTION in nested transactions

-- Bad: Nested BEGIN TRANSACTION without SAVE TRANSACTION
BEGIN TRANSACTION;
    BEGIN TRANSACTION;  -- Nested without savepoint
        SELECT 1;
    COMMIT;
COMMIT;

-- Good: Nested transaction with SAVE TRANSACTION
BEGIN TRANSACTION;
    SAVE TRANSACTION SavePoint1;
    BEGIN TRANSACTION;
        SELECT 1;
    COMMIT;
COMMIT;

-- Good: Single transaction (no nesting)
BEGIN TRANSACTION;
    SELECT 1;
COMMIT;
