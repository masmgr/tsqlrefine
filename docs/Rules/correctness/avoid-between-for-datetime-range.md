# Avoid BETWEEN for Datetime Range

**Rule ID:** `avoid-between-for-datetime-range`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects BETWEEN for datetime ranges. BETWEEN includes both endpoints, which can cause boundary issues with time components.

## Rationale

`BETWEEN` is inclusive on both ends. For datetime columns with time components, this can cause:

- **Double-counting**: Rows exactly at the upper boundary are included in both the current and next range
- **Missing rows**: If the upper bound is a date without time, rows later in the day are excluded

The `>= AND <` pattern (half-open interval) avoids these issues by excluding the upper bound.

This rule detects BETWEEN when column/variable names contain "time" (e.g., CreatedTime, datetime, timestamp), when datetime functions are used (GETDATE, SYSDATETIME, etc.), or when CAST/CONVERT targets datetime types.

## Examples

### Bad

```sql
-- BETWEEN includes both endpoints — rows at exactly @to are included
SELECT * FROM dbo.Orders WHERE CreatedTime BETWEEN @from AND @to;

-- BETWEEN with datetime functions
SELECT * FROM dbo.Orders WHERE OrderDate BETWEEN GETDATE() - 7 AND GETDATE();

-- BETWEEN with CAST to datetime
SELECT * FROM dbo.Orders WHERE CAST(OrderDate AS DATETIME) BETWEEN @from AND @to;
```

### Good

```sql
-- Half-open interval: includes @from, excludes @to
SELECT * FROM dbo.Orders WHERE CreatedTime >= @from AND CreatedTime < @to;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "avoid-between-for-datetime-range", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
