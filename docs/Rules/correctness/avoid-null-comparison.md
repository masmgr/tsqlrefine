# Avoid NULL Comparison

**Rule ID:** `avoid-null-comparison`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects NULL comparisons using `=`, `<>`, or `!=` operators instead of `IS NULL` or `IS NOT NULL`. These comparisons always evaluate to `UNKNOWN` and will not return the expected results.

## Rationale

In SQL, `NULL` represents an unknown or missing value. Comparisons with `NULL` using equality operators have special behavior defined by three-valued logic:

**Why this matters:**
- **Always evaluates to UNKNOWN**: `column = NULL` is neither true nor false, it's UNKNOWN
- **WHERE clause filters UNKNOWN**: Only TRUE predicates pass the WHERE clause; UNKNOWN is treated like FALSE
- **Silent logical errors**: The query runs without error but returns incorrect results
- **Common beginner mistake**: Many developers expect `= NULL` to work like other languages

**SQL three-valued logic:**
- `NULL = NULL` => UNKNOWN (not TRUE!)
- `NULL <> NULL` => UNKNOWN
- `NULL != NULL` => UNKNOWN

**Correct approach:** Use `IS NULL` or `IS NOT NULL` operators which explicitly handle NULL values and return TRUE or FALSE.

## Examples

### Bad

```sql
-- Equals NULL - will never match any rows
SELECT * FROM users WHERE name = NULL;

-- Not equals with <> - will never match any rows
SELECT * FROM users WHERE status <> NULL;

-- Not equals with != - also will never match any rows
SELECT * FROM users WHERE email != NULL;

-- NULL on left side - same problem
SELECT * FROM users WHERE NULL = name;

-- In UPDATE statement - won't update any rows
UPDATE users SET active = 0 WHERE last_login = NULL;

-- In DELETE statement - won't delete any rows
DELETE FROM sessions WHERE user_id <> NULL;

-- Multiple NULL comparisons - none will work correctly
SELECT * FROM users
WHERE name = NULL AND status <> NULL;
```

### Good

```sql
-- IS NULL - correctly identifies NULL values
SELECT * FROM users WHERE name IS NULL;

-- IS NOT NULL - correctly identifies non-NULL values
SELECT * FROM users WHERE status IS NOT NULL;

-- Combined with other conditions
SELECT * FROM users
WHERE name IS NULL AND status = 'active';

-- In UPDATE statement
UPDATE users
SET active = 0
WHERE last_login IS NULL;

-- In DELETE statement
DELETE FROM sessions
WHERE user_id IS NOT NULL AND session_expired = 1;

-- With OR logic
SELECT * FROM users
WHERE email IS NULL OR email = '';

-- Using ISNULL or COALESCE for default values
SELECT * FROM users
WHERE ISNULL(status, 'unknown') = 'active';

-- Checking for NULL or empty string
SELECT * FROM users
WHERE name IS NULL OR name = '';
```

## Understanding ANSI_NULLS

SQL Server has a setting called `ANSI_NULLS` that affects NULL comparison behavior:

```sql
-- Default setting (recommended)
SET ANSI_NULLS ON;
-- Now: column = NULL always returns UNKNOWN
-- Use: column IS NULL

-- Legacy setting (deprecated)
SET ANSI_NULLS OFF;
-- Now: column = NULL can return TRUE
-- Still recommended to use IS NULL for clarity
```

**Important:** Always keep `ANSI_NULLS ON` (the default) for ANSI SQL compliance.

## Common Patterns

### Finding rows with NULL or empty values
```sql
-- Both NULL and empty string
SELECT * FROM users
WHERE name IS NULL OR name = '';

-- Using ISNULL to check both
SELECT * FROM users
WHERE ISNULL(NULLIF(name, ''), 'N/A') = 'N/A';
```

### Excluding NULL values
```sql
-- Explicitly exclude NULLs
SELECT * FROM users WHERE email IS NOT NULL;

-- With additional conditions
SELECT * FROM users
WHERE status = 'active'
  AND last_login IS NOT NULL;
```

### Handling NULL in aggregates
```sql
-- COUNT ignores NULLs automatically
SELECT COUNT(email) FROM users;  -- Counts non-NULL emails

-- Finding rows with NULLs
SELECT * FROM users
WHERE email IS NULL;
```

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "avoid-null-comparison", "enabled": false }
  ]
}
```

## See Also

- SQL three-valued logic documentation
- `ISNULL()` and `COALESCE()` functions for NULL handling
- `ANSI_NULLS` setting documentation
