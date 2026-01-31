# Duplicate Go

**Rule ID:** `duplicate-go`
**Category:** Style
**Severity:** Information
**Fixable:** No

## Description

Avoid consecutive GO batch separators.

## Rationale

Consecutive `GO` batch separators create **empty batches** that serve no purpose and indicate possible errors:

1. **Empty batches**: `GO` followed immediately by another `GO` creates a zero-statement batch
   - No SQL statements execute in that batch
   - Wastes parser and execution overhead

2. **Possible editing errors**:
   - Accidental deletion of code between `GO` statements
   - Copy-paste mistakes leaving extra `GO` statements
   - Automated script generation bugs

3. **Confusing execution flow**:
   - Readers may expect code between `GO` statements
   - Suggests incomplete refactoring or commented-out code
   - Makes debugging script execution order harder

**Note**: `GO` is **not a T-SQL statement** - it's a batch separator recognized by client tools (SSMS, sqlcmd).

**When `GO` is needed:**
- Between DDL statements that must be in separate batches (CREATE PROCEDURE, CREATE VIEW)
- After ALTER DATABASE statements
- To reset variable scope (variables don't cross batch boundaries)

## Examples

### Bad

```sql
-- Consecutive GO statements (empty batch)
SELECT 1;
GO
GO
SELECT 2;

-- Multiple empty batches
CREATE PROCEDURE dbo.Proc1 AS SELECT 1;
GO
GO
GO
CREATE PROCEDURE dbo.Proc2 AS SELECT 2;
GO

-- Suggests missing code
ALTER TABLE users ADD email NVARCHAR(255);
GO
GO  -- Was there supposed to be code here?
CREATE INDEX IX_users_email ON users(email);
GO

-- Script generation error
DROP TABLE IF EXISTS temp_data;
GO
GO  -- Automated script bug
CREATE TABLE temp_data (id INT);
GO
```

### Good

```sql
-- Single GO between batches
SELECT 1;
GO
SELECT 2;

-- Proper batch separation for procedures
CREATE PROCEDURE dbo.Proc1 AS SELECT 1;
GO
CREATE PROCEDURE dbo.Proc2 AS SELECT 2;
GO

-- Clean DDL batching
ALTER TABLE users ADD email NVARCHAR(255);
GO
CREATE INDEX IX_users_email ON users(email);
GO

-- Script with proper batch boundaries
DROP TABLE IF EXISTS temp_data;
GO
CREATE TABLE temp_data (id INT);
GO
INSERT INTO temp_data VALUES (1), (2), (3);
GO

-- No GO needed within same batch
DECLARE @x INT = 1;
SELECT @x;  -- Same batch, no GO

-- GO to reset variable scope
DECLARE @x INT = 1;
SELECT @x;
GO
-- @x is no longer in scope
DECLARE @x INT = 2;  -- New variable with same name
SELECT @x;
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
    { "id": "duplicate-go", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
