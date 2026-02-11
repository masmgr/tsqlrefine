# Normalize Inequality Operator

**Rule ID:** `normalize-inequality-operator`
**Category:** Style
**Severity:** Information
**Fixable:** Yes

## Description

Normalizes != to <> (ISO standard inequality operator).

## Rationale

T-SQL supports two inequality operators: `<>` (ISO/ANSI standard) and `!=` (non-standard). While both are functionally identical, standardizing on `<>` offers several benefits:

1. **ISO compliance**: `<>` is the SQL standard operator recognized by all SQL databases
2. **Portability**: Code using `<>` is more portable across database platforms
3. **Consistency**: Using a single operator style reduces cognitive load during code reviews
4. **Convention alignment**: Most T-SQL coding standards recommend `<>` over `!=`

## Examples

### Bad

```sql
-- Non-standard inequality operator
SELECT * FROM dbo.Users WHERE status != 'active';

-- Multiple uses
SELECT * FROM dbo.Orders
WHERE status != 'pending'
  AND total != 0;

-- In complex expressions
SELECT * FROM dbo.Users
WHERE COALESCE(status, 'unknown') != 'active';
```

### Good

```sql
-- ISO standard inequality operator
SELECT * FROM dbo.Users WHERE status <> 'active';

-- Multiple uses
SELECT * FROM dbo.Orders
WHERE status <> 'pending'
  AND total <> 0;

-- In complex expressions
SELECT * FROM dbo.Users
WHERE COALESCE(status, 'unknown') <> 'active';
```

## Auto-Fix

This rule supports auto-fixing. The `!=` operator will be replaced with `<>`.

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
    { "id": "normalize-inequality-operator", "enabled": false }
  ]
}
```

## See Also

- [avoid-null-comparison](../correctness/avoid-null-comparison.md) - Detects NULL comparisons using = or <>
- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
