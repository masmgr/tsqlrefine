# String Agg Without Order By

**Rule ID:** `string-agg-without-order-by`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects STRING_AGG without WITHIN GROUP (ORDER BY), which may produce non-deterministic string concatenation results.

## Rationale

The `STRING_AGG` function (SQL Server 2017+) aggregates string values into a single concatenated result. Without the `WITHIN GROUP (ORDER BY ...)` clause, the order of concatenated values is not guaranteed. This means:

1. **Non-deterministic results**: The same query may return values in different orders between executions
2. **Inconsistent behavior**: Results may vary across different SQL Server instances, hardware, or after index changes
3. **Hard-to-debug issues**: Intermittent failures in applications that depend on consistent ordering

Always specify `WITHIN GROUP (ORDER BY ...)` to ensure deterministic, reproducible results.

## Examples

### Bad

```sql
-- No ORDER BY - results may vary between executions
SELECT STRING_AGG(name, ',') AS names FROM users;

-- With GROUP BY but no ORDER BY
SELECT id, STRING_AGG(tag, '; ') AS tags
FROM items
GROUP BY id;

-- In subquery without ORDER BY
SELECT *
FROM (
    SELECT id, STRING_AGG(tag, ',') AS tags
    FROM items
    GROUP BY id
) AS sub;
```

### Good

```sql
-- With WITHIN GROUP (ORDER BY) - deterministic results
SELECT STRING_AGG(name, ',') WITHIN GROUP (ORDER BY name) AS names
FROM users;

-- With GROUP BY and ORDER BY
SELECT id, STRING_AGG(tag, '; ') WITHIN GROUP (ORDER BY tag ASC) AS tags
FROM items
GROUP BY id;

-- Descending order
SELECT STRING_AGG(name, ', ') WITHIN GROUP (ORDER BY created_at DESC) AS recent_names
FROM users;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "string-agg-without-order-by", "enabled": false }
  ]
}
```

## Notes

- This rule only applies to SQL Server 2017+ (compatibility level 140+)
- For older SQL Server versions using STUFF with FOR XML PATH, see [stuff-without-order-by](stuff-without-order-by.md)

## See Also

- [stuff-without-order-by](stuff-without-order-by.md) - Similar rule for STUFF with FOR XML PATH
- [prefer-string-agg-over-stuff](../style/prefer-string-agg-over-stuff.md) - Recommends STRING_AGG over STUFF for SQL Server 2017+
- [TsqlRefine Rules Documentation](../README.md)
