# Semantic Schema Qualify

**Rule ID:** `semantic/schema-qualify`
**Category:** Style
**Severity:** Warning
**Fixable:** No

## Description

Requires all table references to include schema qualification (e.g., dbo.Users) for clarity and to avoid ambiguity.

## Rationale

Omitting schema qualification (e.g., `Users` instead of `dbo.Users`) creates **ambiguity, performance issues, and maintenance problems**:

1. **Schema resolution overhead**:
   - SQL Server must search for the object in the user's default schema first
   - Then searches in `dbo` schema
   - This lookup adds overhead to every query

2. **Ambiguity and errors**:
   - Multiple schemas may contain tables with the same name
   - Different users may have different default schemas
   - Query may access the wrong table depending on who executes it

3. **Stored procedure plan cache pollution**:
   - Queries without schema qualification generate different execution plans per user
   - Reduces plan reuse efficiency
   - Wastes memory in plan cache

4. **Maintenance confusion**:
   - Difficult to determine which schema contains the object
   - Refactoring becomes error-prone (which `Users` table?)
   - Documentation and code reviews are harder

**Best practices:**
- **Always qualify** with schema name (usually `dbo`)
- **Explicit is better** than relying on default schema
- **Consistent qualification** across codebase

## Examples

### Bad

```sql
-- Unqualified table name (schema lookup overhead)
SELECT * FROM Users;

-- Multiple tables without schema
SELECT u.username, o.order_date
FROM Users u
INNER JOIN Orders o ON u.user_id = o.user_id;

-- Procedure without schema (may find wrong procedure)
EXEC GetUserById @UserId = 123;

-- View without schema
SELECT * FROM vw_ActiveUsers;

-- Function without schema
SELECT dbo.FormatCurrency(SalesAmount) AS FormattedAmount
FROM SalesData;  -- Table not qualified, function is

-- Ambiguous: Which Users table?
-- Could be dbo.Users, sales.Users, or audit.Users
UPDATE Users SET status = 'inactive';
```

### Good

```sql
-- Fully qualified table name
SELECT * FROM dbo.Users;

-- All tables qualified
SELECT u.username, o.order_date
FROM dbo.Users u
INNER JOIN dbo.Orders o ON u.user_id = o.user_id;

-- Procedure with schema
EXEC dbo.GetUserById @UserId = 123;

-- View with schema
SELECT * FROM dbo.vw_ActiveUsers;

-- Both table and function qualified
SELECT dbo.FormatCurrency(s.SalesAmount) AS FormattedAmount
FROM dbo.SalesData s;

-- Clear: Exactly which table
UPDATE dbo.Users SET status = 'inactive';

-- Cross-schema access (explicit intent)
SELECT u.username, a.action
FROM dbo.Users u
INNER JOIN audit.UserActions a ON u.user_id = a.user_id;

-- Temporary tables (no schema needed)
SELECT * FROM #TempUsers;  -- Temp tables don't need schema

-- Table variables (no schema needed)
DECLARE @UserTable TABLE (user_id INT, username NVARCHAR(50));
SELECT * FROM @UserTable;
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
    { "id": "semantic-schema-qualify", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
