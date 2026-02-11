# Semantic Case Sensitive Variables

**Rule ID:** `semantic/case-sensitive-variables`
**Category:** Style
**Severity:** Warning
**Fixable:** No

## Description

Ensures variable references match the exact casing used in their declarations for consistency.

## Rationale

While T-SQL is **case-insensitive** for variable names (both `@UserName` and `@USERNAME` refer to the same variable), **inconsistent casing reduces code quality**:

1. **Readability issues**:
   - Inconsistent casing makes code harder to scan visually
   - Readers may think different casings refer to different variables
   - Increases cognitive load when reading the code

2. **Maintenance problems**:
   - Searching for variable usage becomes difficult (case-sensitive search fails)
   - Refactoring tools may not handle inconsistent casing well
   - Code reviews become more difficult

3. **Professionalism**: Inconsistent casing signals careless or rushed development

4. **Misleading in other contexts**:
   - SQL Server object names CAN be case-sensitive (depending on collation)
   - Developers coming from case-sensitive languages may be confused
   - Variables in application code (C#, etc.) ARE case-sensitive

**Best practices:**
- **Choose a naming convention** (PascalCase, camelCase, snake_case)
- **Be consistent** throughout the file/project
- **Match the declaration** when referencing variables

## Examples

### Bad

```sql
-- Inconsistent casing within same procedure
DECLARE @UserName NVARCHAR(50);
DECLARE @UserID INT;

SET @USERNAME = 'John';      -- All caps
SET @userid = 123;            -- All lowercase
SET @UserName = @username;    -- Mixed with lowercase

IF @USERID > 0                -- All caps again
BEGIN
    SELECT @username;          -- Lowercase
END;

-- Different casing in different locations
DECLARE @TotalAmount DECIMAL(10, 2);
SET @totalamount = 100.50;    -- Lowercase
UPDATE orders SET total = @TOTALAMOUNT;  -- Uppercase

-- Makes searching difficult
DECLARE @OrderStatus VARCHAR(20);
SET @orderstatus = 'pending';  -- Won't find with case-sensitive search
```

### Good

```sql
-- Consistent PascalCase throughout
DECLARE @UserName NVARCHAR(50);
DECLARE @UserID INT;

SET @UserName = 'John';
SET @UserID = 123;

IF @UserID > 0
BEGIN
    SELECT @UserName;
END;

-- Consistent camelCase throughout
DECLARE @totalAmount DECIMAL(10, 2);
SET @totalAmount = 100.50;
UPDATE orders SET total = @totalAmount;

-- Consistent snake_case (less common in T-SQL)
DECLARE @order_status VARCHAR(20);
SET @order_status = 'pending';
SELECT @order_status;

-- Best: Match the declaration exactly
DECLARE @CustomerEmail NVARCHAR(255);
DECLARE @OrderDate DATETIME;

SET @CustomerEmail = 'customer@example.com';  -- Exact match
SET @OrderDate = GETDATE();                    -- Exact match

SELECT @CustomerEmail, @OrderDate;             -- Exact match
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
    { "id": "semantic-case-sensitive-variables", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
