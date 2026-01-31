# Nested Block Comments

**Rule ID:** `nested-block-comments`
**Category:** Style
**Severity:** Warning
**Fixable:** No

## Description

Avoid nested block comments (/* /* */ */).

## Rationale

Nested block comments (`/* /* */ */`) cause parsing issues and unexpected behavior in SQL Server:

1. **SQL Server does not support nesting**: Unlike some languages (e.g., C), T-SQL treats `/*` and `*/` as simple delimiters
   - The first `*/` closes the entire comment, regardless of how many `/*` appeared before
   - Subsequent code may be unexpectedly uncommented

2. **Confusing behavior**:
   ```sql
   /* outer /* inner */ outer */
   SELECT 1;
   ```
   - Developer expects: Both comments closed, `SELECT 1` executes
   - Actual behavior: Comment closes at first `*/`, `outer */` becomes syntax error

3. **Maintenance hazards**:
   - Commenting out large blocks that already contain comments
   - Copy-paste errors when merging code with comments
   - Difficult to debug when code is unexpectedly active

4. **Refactoring problems**: Automated tools and IDEs may not handle nested comments correctly

**Better alternatives:**
- Use `--` line comments instead of nesting block comments
- Remove inner comments before wrapping in block comment
- Use IDE comment/uncomment features (which handle nesting correctly)

## Examples

### Bad

```sql
-- Nested block comment (unexpected behavior)
/* outer comment
    /* inner comment */
    outer comment continues here
*/
SELECT 1;  -- Syntax error: "outer comment continues here */" is not commented

-- Commenting out code with existing block comments
/*
SELECT * FROM users;  /* Get all users */
SELECT * FROM orders;  /* Get all orders */
*/
-- Result: Everything after first "*/" is active code!

-- Multiple nesting levels (even worse)
/* level 1
    /* level 2
        /* level 3 */ -- Closes at level 3
    level 2 continues */ -- Syntax error
level 1 continues */ -- Also syntax error
```

### Good

```sql
-- Use line comments instead
-- outer comment
--   inner comment
--   outer comment continues here
SELECT 1;

-- Comment out blocks using line comments
-- SELECT * FROM users;  -- Get all users
-- SELECT * FROM orders;  -- Get all orders

-- Or remove inner comments before block commenting
/*
SELECT * FROM users;
SELECT * FROM orders;
*/

-- Use IDE features for block commenting (handles nesting)
-- Many IDEs convert nested comments to line comments automatically

-- Alternative: Use #region (not standard T-SQL, but supported by some tools)
--#region User queries
SELECT * FROM users;  -- Get all users
SELECT * FROM orders;  -- Get all orders
--#endregion
```

## Configuration

To disable this rule, add it to your `tsqlrefine.json`:

```json
{
  "ruleset": "custom-ruleset.json"
}
```

In `custom-ruleset.json`:

```json
{
  "rules": [
    { "id": "nested-block-comments", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
