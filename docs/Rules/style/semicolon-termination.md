# Semicolon Termination

**Rule ID:** `semicolon-termination`
**Category:** Style
**Severity:** Information
**Fixable:** Yes

## Description

SQL statements should be terminated with a semicolon

## Rationale

Terminating SQL statements with semicolons (`;`) is a **best practice** for consistency and future compatibility:

1. **ANSI SQL standard**: Semicolons are the **standard** statement terminator in SQL
   - Used by PostgreSQL, MySQL, Oracle, and most other databases
   - Makes T-SQL code more portable

2. **Required for some T-SQL statements**:
   - **WITH (CTE)**: Must be preceded by semicolon if previous statement lacks one
   - **THROW**: Requires semicolon terminator in some contexts
   - **MERGE**: Often requires semicolon terminator
   - Prevents ambiguous syntax errors

3. **Future-proofing**: Microsoft recommends semicolon usage
   - Future SQL Server versions may require semicolons more strictly
   - Microsoft documentation includes semicolons in all examples

4. **Consistency**: Using semicolons everywhere eliminates "do I need one here?" questions
   - Every statement ends the same way
   - Reduces cognitive load

5. **Prevents CTE errors**: Most common issue is missing semicolon before WITH
   ```sql
   SELECT 1  -- Missing semicolon
   WITH cte AS (...)  -- Error: Incorrect syntax near 'WITH'
   ```

## Examples

### Bad

```sql
-- Missing semicolons
SELECT 1
SELECT 2
SELECT 3

-- Causes error with CTE
SELECT * FROM users
WITH order_summary AS (
    SELECT user_id, COUNT(*) AS order_count
    FROM orders
    GROUP BY user_id
)
SELECT * FROM order_summary
-- Error: Incorrect syntax near 'WITH'

-- Stored procedure without semicolons
CREATE PROCEDURE dbo.GetUser
    @UserId INT
AS
BEGIN
    SELECT * FROM users WHERE user_id = @UserId
END

-- Multiple statements without semicolons (hard to read)
DECLARE @x INT
SET @x = 1
SELECT @x
```

### Good

```sql
-- All statements terminated with semicolons
SELECT 1;
SELECT 2;
SELECT 3;

-- CTE works correctly
SELECT * FROM users;

WITH order_summary AS (
    SELECT user_id, COUNT(*) AS order_count
    FROM orders
    GROUP BY user_id
)
SELECT * FROM order_summary;

-- Stored procedure with semicolons
CREATE PROCEDURE dbo.GetUser
    @UserId INT
AS
BEGIN
    SELECT * FROM users WHERE user_id = @UserId;
END;

-- Multiple statements with semicolons (readable)
DECLARE @x INT;
SET @x = 1;
SELECT @x;

-- Complex script with consistent semicolons
DECLARE @StartDate DATE = '2024-01-01';
DECLARE @EndDate DATE = '2024-12-31';

WITH sales_summary AS (
    SELECT
        customer_id,
        SUM(total) AS total_sales,
        COUNT(*) AS order_count
    FROM orders
    WHERE order_date BETWEEN @StartDate AND @EndDate
    GROUP BY customer_id
)
SELECT
    c.customer_name,
    ss.total_sales,
    ss.order_count
FROM customers AS c
INNER JOIN sales_summary AS ss ON c.customer_id = ss.customer_id
WHERE ss.total_sales > 1000
ORDER BY ss.total_sales DESC;

-- MERGE statement with semicolon (required)
MERGE INTO target_table AS t
USING source_table AS s ON t.id = s.id
WHEN MATCHED THEN
    UPDATE SET t.value = s.value
WHEN NOT MATCHED THEN
    INSERT (id, value) VALUES (s.id, s.value);

-- THROW statement with semicolon
IF @ErrorCondition = 1
BEGIN
    THROW 50001, 'Error message', 1;
END;
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
    { "id": "semicolon-termination", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
