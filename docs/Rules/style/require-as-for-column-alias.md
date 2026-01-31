# Require As For Column Alias

**Rule ID:** `require-as-for-column-alias`
**Category:** Style
**Severity:** Information
**Fixable:** Yes

## Description

Column aliases should use the AS keyword

## Rationale

Omitting `AS` keyword in column aliases makes code **harder to read and can cause confusion**:

1. **Readability**: `AS` makes the alias explicit and clear
   - `SELECT id userId` looks like two separate columns
   - `SELECT id AS userId` clearly shows `userId` is an alias for `id`

2. **Distinguishes from multi-column SELECT**:
   - `SELECT id, name FROM users` (two columns)
   - `SELECT id name FROM users` (single column with alias? or missing comma?)
   - `SELECT id AS name FROM users` (clearly an alias)

3. **Prevents ambiguity** when reading SQL:
   - Without `AS`, readers must infer whether it's an alias or a syntax error
   - With `AS`, intent is immediately clear

4. **Consistent with table aliases**: Table aliases use `FROM users AS u`, so column aliases should use `AS` too

5. **Standard SQL**: While optional in T-SQL, `AS` is more explicit and portable

**Best practice**: Always use `AS` for column aliases to improve code clarity.

## Examples

### Bad

```sql
-- Missing AS (looks like two columns)
SELECT id userId FROM users;

-- Multiple aliases without AS (confusing)
SELECT id userId, name userName, email userEmail FROM users;

-- Hard to distinguish from column list
SELECT
    user_id,
    first_name fullName,  -- Is this an alias or missing comma?
    email
FROM users;

-- Complex expression without AS (unclear)
SELECT COUNT(*) totalUsers FROM users;

-- Nested query without AS
SELECT user_id, (
    SELECT COUNT(*) FROM orders o WHERE o.user_id = u.user_id
) orderCount
FROM users u;
```

### Good

```sql
-- With AS (clear alias)
SELECT id AS userId FROM users;

-- Multiple aliases with AS (readable)
SELECT id AS userId, name AS userName, email AS userEmail FROM users;

-- Clear distinction from column list
SELECT
    user_id,
    first_name AS fullName,  -- Clearly an alias
    email
FROM users;

-- Complex expression with AS (intent clear)
SELECT COUNT(*) AS totalUsers FROM users;

-- Nested query with AS
SELECT user_id, (
    SELECT COUNT(*) FROM orders o WHERE o.user_id = u.user_id
) AS orderCount
FROM users u;

-- Computed columns with AS
SELECT
    first_name + ' ' + last_name AS full_name,
    YEAR(GETDATE()) - YEAR(birth_date) AS age,
    CASE
        WHEN status = 'A' THEN 'Active'
        WHEN status = 'I' THEN 'Inactive'
        ELSE 'Unknown'
    END AS status_label
FROM users;

-- Aggregate functions with AS
SELECT
    department_id,
    COUNT(*) AS employee_count,
    AVG(salary) AS average_salary,
    MAX(salary) AS max_salary,
    MIN(salary) AS min_salary
FROM employees
GROUP BY department_id;
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
    { "id": "require-as-for-column-alias", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
