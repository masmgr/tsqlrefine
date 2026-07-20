# String Agg Nvarchar Max

**Rule ID:** `string-agg-nvarchar-max`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects STRING_AGG whose first argument is not explicitly cast to NVARCHAR(MAX), which risks intermediate result truncation (8000-byte / 4000-char limit).

## Rationale

When the first argument of `STRING_AGG` is not `NVARCHAR(MAX)`, SQL Server infers the type of the concatenated intermediate result from the input expression. If the input is `VARCHAR`, `NVARCHAR(n)`, or another sized/non-Unicode type, the aggregated result is implicitly capped at 8000 bytes / 4000 characters. This causes:

1. **Runtime errors**: `STRING_AGG aggregation result exceeded the limit of 8000 bytes` once concatenated values grow large
2. **Silent truncation**: In some cases the result is quietly cut off instead of erroring, producing wrong data
3. **Encoding loss**: Non-Unicode types (`VARCHAR`) lose characters outside the code page

Explicitly wrapping the first argument in `CAST(... AS NVARCHAR(MAX))` or `CONVERT(NVARCHAR(MAX), ...)` forces the intermediate result to use the unbounded LOB type, avoiding these problems.

## Examples

### Bad

```sql
-- Bare column reference - type inferred from input, may truncate
SELECT STRING_AGG(name, ',') AS names FROM users;

-- Cast to a non-Unicode type
SELECT STRING_AGG(CAST(id AS VARCHAR(10)), ',') AS ids FROM users;

-- Sized nvarchar(n) still truncates
SELECT STRING_AGG(CAST(name AS NVARCHAR(100)), ',') AS names FROM users;

-- varchar(max) is non-Unicode
SELECT STRING_AGG(CONVERT(VARCHAR(MAX), name), ',') AS names FROM users;
```

### Good

```sql
-- Explicit CAST to NVARCHAR(MAX)
SELECT STRING_AGG(CAST(name AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY name) AS names
FROM users;

-- Explicit CONVERT to NVARCHAR(MAX)
SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), name), ',') WITHIN GROUP (ORDER BY name) AS names
FROM users;
```

## Configuration

To disable this rule:

```json
{
  "rules": {
    "string-agg-nvarchar-max": "none"
  }
}
```

## Notes

- This rule only applies to SQL Server 2017+ (compatibility level 140+)
- `TRY_CAST` / `TRY_CONVERT` to `NVARCHAR(MAX)` also satisfy the rule

## See Also

- [string-agg-without-order-by](string-agg-without-order-by.md) - Detects STRING_AGG without deterministic ordering
- [TsqlRefine Rules Documentation](../README.md)
