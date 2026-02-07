-- unreachable-case-when: Detects duplicate WHEN conditions in CASE expressions

-- Bad: Simple CASE with duplicate WHEN values
SELECT CASE @status
    WHEN 1 THEN 'Active'
    WHEN 2 THEN 'Inactive'
    WHEN 1 THEN 'Duplicate'  -- Unreachable: already matched above
END;

-- Bad: Searched CASE with duplicate conditions
SELECT CASE
    WHEN @x = 1 THEN 'First'
    WHEN @x = 2 THEN 'Second'
    WHEN @x = 1 THEN 'Never reached'  -- Unreachable
END;

-- Good: Distinct WHEN values
SELECT CASE @status
    WHEN 1 THEN 'Active'
    WHEN 2 THEN 'Inactive'
    WHEN 3 THEN 'Pending'
END;

-- Good: Distinct searched conditions
SELECT CASE
    WHEN @x = 1 THEN 'First'
    WHEN @x = 2 THEN 'Second'
    WHEN @x = 3 THEN 'Third'
END;
