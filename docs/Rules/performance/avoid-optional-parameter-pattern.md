# Avoid Optional Parameter Pattern

**Rule ID:** `avoid-optional-parameter-pattern`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Detects optional parameter patterns (`@p IS NULL OR col = @p`) and (`col = ISNULL(@p, col)`) which prevent index usage and cause plan instability.

## Rationale

The "catch-all query" or "optional parameter" pattern is one of the most common performance anti-patterns in SQL Server. It appears in two main forms:

1. **`(@p IS NULL OR col = @p)`** — The OR forces SQL Server to create a plan that works for both NULL and non-NULL parameter values
2. **`col = ISNULL(@p, col)`** — The function call on the column prevents index seeks

Both patterns cause:
- **Parameter sniffing issues**: The cached plan may be optimal for NULL but terrible for non-NULL, or vice versa
- **Index scan instead of seek**: The optimizer cannot use indexes efficiently
- **Plan instability**: Different parameter values produce wildly different performance

## Examples

### Bad

```sql
-- Pattern A: IS NULL OR
SELECT * FROM dbo.Users
WHERE (@Name IS NULL OR Name = @Name)
  AND (@Status IS NULL OR Status = @Status);

-- Pattern B: ISNULL
SELECT * FROM dbo.Users
WHERE CustomerId = ISNULL(@CustId, CustomerId);
```

### Good

```sql
-- Dynamic SQL with parameters
DECLARE @sql NVARCHAR(MAX) = N'SELECT * FROM dbo.Users WHERE 1=1';
IF @Name IS NOT NULL SET @sql += N' AND Name = @Name';
IF @Status IS NOT NULL SET @sql += N' AND Status = @Status';
EXEC sp_executesql @sql, N'@Name NVARCHAR(100), @Status INT', @Name, @Status;

-- OPTION (RECOMPILE) for simple cases
SELECT * FROM dbo.Users
WHERE (@Name IS NULL OR Name = @Name)
OPTION (RECOMPILE);
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "avoid-optional-parameter-pattern", "enabled": false }
  ]
}
```

## See Also

- [non-sargable](non-sargable.md) - Detects functions on columns in predicates
- [TsqlRefine Rules Documentation](../README.md)
