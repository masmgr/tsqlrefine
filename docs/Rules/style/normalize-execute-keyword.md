# Normalize Execute Keyword

**Rule ID:** `normalize-execute-keyword`
**Category:** Style
**Severity:** Information
**Fixable:** Yes

## Description

Normalizes 'EXEC' to 'EXECUTE' for consistency.

## Rationale

T-SQL allows both `EXEC` and `EXECUTE` to invoke stored procedures. While both are functionally equivalent, using the full `EXECUTE` keyword consistently improves code readability and clarity:

1. **Consistency**: Using the full keyword form throughout a codebase makes the code more uniform and easier to read
2. **Clarity**: `EXECUTE` is more explicit and self-documenting than the abbreviated `EXEC`
3. **Searchability**: Consistent keyword usage makes it easier to search and grep through code
4. **Standards alignment**: Many coding standards prefer full keyword forms for clarity

## Examples

### Bad

```sql
-- Abbreviated EXEC keyword
EXEC dbo.GetUsers;

-- With parameters
EXEC dbo.UpdateUser @UserId = 1, @Name = N'John';

-- Dynamic SQL execution (abbreviated)
EXEC('SELECT 1');

-- In stored procedure
CREATE PROCEDURE dbo.ProcessOrders
AS
BEGIN
    EXEC dbo.ValidateOrders;
    EXEC dbo.CalculateTotals;
    EXEC dbo.SendNotifications;
END;
```

### Good

```sql
-- Full EXECUTE keyword
EXECUTE dbo.GetUsers;

-- With parameters
EXECUTE dbo.UpdateUser @UserId = 1, @Name = N'John';

-- Dynamic SQL execution (full form)
EXECUTE('SELECT 1');

-- In stored procedure
CREATE PROCEDURE dbo.ProcessOrders
AS
BEGIN
    EXECUTE dbo.ValidateOrders;
    EXECUTE dbo.CalculateTotals;
    EXECUTE dbo.SendNotifications;
END;
```

## Auto-Fix

This rule supports auto-fixing. The `EXEC` keyword will be replaced with `EXECUTE`.

To apply the fix:

```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- fix file.sql
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
    { "id": "normalize-execute-keyword", "enabled": false }
  ]
}
```

## See Also

- [normalize-procedure-keyword](normalize-procedure-keyword.md) - Normalizes PROC to PROCEDURE
- [normalize-transaction-keyword](normalize-transaction-keyword.md) - Normalizes TRAN to TRANSACTION
- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
