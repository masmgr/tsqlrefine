# Set Ansi

**Rule ID:** `set-ansi`
**Category:** Configuration
**Severity:** Warning
**Fixable:** No

## Description

Files should start with SET ANSI_NULLS ON within the first 10 statements.

## Rationale

Setting `ANSI_NULLS ON` at the beginning of scripts is **required for proper NULL handling** and is a **best practice** for SQL Server development:

1. **Required for indexed views and computed columns**:
   - SQL Server **requires** `ANSI_NULLS ON` when creating indexes on views
   - Computed columns with indexes also require this setting
   - Without it, you get an error: "Cannot create index on view... because it was created with ANSI_NULLS OFF"

2. **NULL comparison behavior**:
   - `ANSI_NULLS ON` (recommended): `NULL = NULL` returns `NULL` (unknown), not `TRUE`
   - `ANSI_NULLS OFF` (deprecated): `NULL = NULL` returns `TRUE` (non-standard behavior)
   - ANSI standard requires `ANSI_NULLS ON`

3. **Future compatibility**:
   - Microsoft **deprecated** `ANSI_NULLS OFF` in SQL Server 2008
   - Future versions may **remove** support for `ANSI_NULLS OFF`
   - New code should always use `ANSI_NULLS ON`

4. **SET settings baked into objects**:
   - When creating stored procedures, functions, views, or triggers, the current SET options are **saved with the object**
   - The object uses these settings when executed, regardless of the session's settings
   - Must be set **before** CREATE PROCEDURE/FUNCTION/VIEW

**Best practices:**
- Set `ANSI_NULLS ON` at the top of every script file
- Set `QUOTED_IDENTIFIER ON` as well (often required together)
- Use within the first 10 statements (before any object creation)

## Examples

### Bad

```sql
-- No ANSI_NULLS setting (uses session default, may be OFF)
CREATE PROCEDURE dbo.GetUsers AS
BEGIN
    SELECT * FROM users WHERE deleted_at = NULL;  -- May not work as expected
END;
GO

-- ANSI_NULLS set after object creation (too late!)
CREATE PROCEDURE dbo.UpdateUser AS
BEGIN
    UPDATE users SET name = 'John';
END;
GO
SET ANSI_NULLS ON;  -- Does not affect the procedure created above

-- Missing from file with indexed view
CREATE VIEW dbo.vw_ActiveUsers
WITH SCHEMABINDING
AS
    SELECT user_id, username
    FROM dbo.users
    WHERE status = 'active';
GO
CREATE UNIQUE CLUSTERED INDEX IX_vw_ActiveUsers ON dbo.vw_ActiveUsers(user_id);
-- Error: Cannot create index because ANSI_NULLS was OFF
```

### Good

```sql
-- Set ANSI_NULLS ON at the beginning of script
SET ANSI_NULLS ON;
GO

CREATE PROCEDURE dbo.GetUsers AS
BEGIN
    SELECT * FROM users WHERE deleted_at IS NULL;  -- Correct NULL check
END;
GO

-- Both ANSI_NULLS and QUOTED_IDENTIFIER (best practice)
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE PROCEDURE dbo.UpdateUser
    @UserId INT,
    @Name NVARCHAR(100)
AS
BEGIN
    UPDATE users
    SET name = @Name
    WHERE user_id = @UserId;
END;
GO

-- Indexed view with proper settings
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE VIEW dbo.vw_ActiveUsers
WITH SCHEMABINDING
AS
    SELECT user_id, username, email
    FROM dbo.users
    WHERE status = 'active';
GO

CREATE UNIQUE CLUSTERED INDEX IX_vw_ActiveUsers ON dbo.vw_ActiveUsers(user_id);
GO

-- Complete script with all recommended settings
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
GO

CREATE FUNCTION dbo.GetUserCount()
RETURNS INT
AS
BEGIN
    DECLARE @Count INT;
    SELECT @Count = COUNT(*) FROM dbo.users;
    RETURN @Count;
END;
GO
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
    { "id": "set-ansi", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
