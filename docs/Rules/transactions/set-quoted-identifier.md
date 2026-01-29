# Set Quoted Identifier

**Rule ID:** `set-quoted-identifier`
**Category:** Configuration
**Severity:** Warning
**Fixable:** No

## Description

Files should start with SET QUOTED_IDENTIFIER ON within the first 10 statements to ensure proper identifier handling.

## Rationale

SET QUOTED_IDENTIFIER ON is required for many SQL Server features and ensures standard SQL compliance.

**Why QUOTED_IDENTIFIER ON is required**:

1. **Indexed views**: Cannot create indexed views with QUOTED_IDENTIFIER OFF
2. **Computed columns**: Persisted computed columns require QUOTED_IDENTIFIER ON
3. **Filtered indexes**: Must be created with QUOTED_IDENTIFIER ON
4. **XML indexes**: Require QUOTED_IDENTIFIER ON
5. **Standard SQL compliance**: ON is SQL standard behavior

**Behavior difference**:

| QUOTED_IDENTIFIER | Double quotes (") | Behavior |
|-------------------|-------------------|----------|
| ON (recommended) | Identifier delimiter | `"Order"` is column/table name |
| OFF (legacy) | String delimiter | `"Order"` is string literal |

**Error when OFF**:
```
Msg 1935, Level 16, State 1
Cannot create index on view 'dbo.vw_Sales' because the view was created with QUOTED_IDENTIFIER OFF.
```

**Best practice**: Always use SET QUOTED_IDENTIFIER ON at the beginning of SQL files for:
- Views (especially indexed views)
- Stored procedures
- Functions
- DDL scripts

## Examples

### Bad

```sql
-- File without SET QUOTED_IDENTIFIER ON
CREATE VIEW vw_ActiveUsers AS
SELECT "Order", "User" FROM Users WHERE Active = 1;
-- Ambiguous: Are these column names or string literals?

-- Attempting to create indexed view will fail
CREATE UNIQUE CLUSTERED INDEX IX_ActiveUsers ON vw_ActiveUsers("Order");
-- Error: Cannot create index because view was created without QUOTED_IDENTIFIER ON
```

### Good

```sql
-- SET QUOTED_IDENTIFIER ON at file start
SET QUOTED_IDENTIFIER ON;

CREATE VIEW vw_ActiveUsers AS
SELECT [Order], [User] FROM Users WHERE Active = 1;  -- Unambiguous with brackets

-- Indexed view creation succeeds
CREATE UNIQUE CLUSTERED INDEX IX_ActiveUsers ON vw_ActiveUsers([Order]);

-- Stored procedure with QUOTED_IDENTIFIER ON
CREATE PROCEDURE uspGetOrders
AS
BEGIN
    SET NOCOUNT ON;
    SELECT OrderId, "OrderDate" FROM Orders;  -- Double quotes work as identifier delimiter
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
    { "id": "set-quoted-identifier", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
