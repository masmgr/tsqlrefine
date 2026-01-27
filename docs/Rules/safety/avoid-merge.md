# Avoid MERGE

**Rule ID:** `avoid-merge`
**Category:** Safety
**Severity:** Warning
**Fixable:** No

## Description

Warns against using the `MERGE` statement due to known bugs, unpredictable behavior, and complexity. Recommends using separate `INSERT`, `UPDATE`, and `DELETE` statements instead.

## Rationale

Despite being a SQL standard feature, SQL Server's `MERGE` implementation has significant issues:

**Known problems:**
- **Data corruption bugs**: Multiple documented bugs that can cause incorrect data updates
- **Race conditions**: Can violate constraints in concurrent scenarios even with proper locking
- **Unexpected duplicates**: Can insert duplicate rows that violate unique constraints
- **Complex locking**: Requires careful understanding of locking hints to work correctly
- **Difficult debugging**: Errors in MERGE are harder to troubleshoot than separate statements

**Performance concerns:**
- Often performs worse than equivalent separate `INSERT`/`UPDATE`/`DELETE` statements
- Optimizer struggles to create efficient plans for complex MERGE operations
- Difficult to tune and optimize

**Maintainability issues:**
- Complex syntax makes code harder to understand and maintain
- Mixing INSERT, UPDATE, and DELETE logic in one statement reduces clarity
- Error handling is more complicated than separate statements

**Microsoft's recommendation:** Many SQL Server experts, including Microsoft MVPs, recommend avoiding MERGE in production code.

## Examples

### Bad

```sql
-- Simple MERGE - looks clean but has hidden issues
MERGE INTO target t
USING source s
ON t.id = s.id
WHEN MATCHED THEN
    UPDATE SET t.value = s.value;

-- MERGE with INSERT - can violate constraints under concurrency
MERGE INTO users t
USING new_users s ON t.id = s.id
WHEN MATCHED THEN
    UPDATE SET t.name = s.name
WHEN NOT MATCHED THEN
    INSERT (id, name) VALUES (s.id, s.name);

-- Complex MERGE with multiple conditions - hard to debug
MERGE INTO target t
USING source s ON t.id = s.id
WHEN MATCHED AND t.status = 'active' THEN
    UPDATE SET t.value = s.value
WHEN MATCHED AND t.status = 'deleted' THEN
    DELETE
WHEN NOT MATCHED THEN
    INSERT (id, value) VALUES (s.id, s.value);
```

### Good

```sql
-- Separate UPDATE and INSERT - clearer and more reliable
BEGIN TRANSACTION;

-- Update existing records
UPDATE t
SET t.value = s.value
FROM target t
INNER JOIN source s ON t.id = s.id;

-- Insert new records
INSERT INTO target (id, value)
SELECT s.id, s.value
FROM source s
WHERE NOT EXISTS (SELECT 1 FROM target t WHERE t.id = s.id);

COMMIT TRANSACTION;

-- Alternative: Use EXISTS for conditional logic
UPDATE users
SET name = (SELECT name FROM new_users WHERE id = users.id)
WHERE EXISTS (SELECT 1 FROM new_users WHERE id = users.id);

INSERT INTO users (id, name)
SELECT id, name
FROM new_users
WHERE NOT EXISTS (SELECT 1 FROM users WHERE id = new_users.id);

-- With proper error handling
BEGIN TRY
    BEGIN TRANSACTION;

    -- Update
    UPDATE target
    SET value = source.value
    FROM target
    INNER JOIN source ON target.id = source.id;

    -- Insert
    INSERT INTO target (id, value)
    SELECT id, value
    FROM source
    WHERE NOT EXISTS (SELECT 1 FROM target WHERE target.id = source.id);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
```

## When MERGE Might Be Considered

If you absolutely must use MERGE (e.g., legacy system requirement):

```sql
-- Use explicit serializable isolation to prevent race conditions
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

MERGE INTO target WITH (HOLDLOCK) AS t
USING source AS s ON t.id = s.id
WHEN MATCHED THEN
    UPDATE SET t.value = s.value
WHEN NOT MATCHED THEN
    INSERT (id, value) VALUES (s.id, s.value);

COMMIT TRANSACTION;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
```

**Note:** Even with proper isolation, MERGE should still be avoided due to its other issues.

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "avoid-merge", "enabled": false }
  ]
}
```

## Further Reading

- [Microsoft Connect: MERGE bugs](https://connect.microsoft.com/SQLServer/feedback/details/704819)
- SQL Server MVP recommendations against using MERGE
- Various blog posts documenting MERGE issues and alternatives

## See Also

- [dml-without-where](dml-without-where.md) - Prevents unsafe UPDATE/DELETE without WHERE clause
