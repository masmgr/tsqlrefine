# Avoid OR On Different Columns

**Rule ID:** `avoid-or-on-different-columns`
**Category:** Performance
**Severity:** Information
**Fixable:** No

## Description

Detects OR conditions on different columns in WHERE, JOIN ON, or HAVING predicates which may prevent index usage and cause table scans. Only flags simple comparison patterns where both sides of OR reference different columns.

## Rationale

When OR conditions span different columns, SQL Server often cannot use indexes efficiently:

**Performance problems:**
- **Index selection difficulty**: A single index typically covers one column; OR on different columns may require scanning both
- **Table/index scan**: The optimizer may fall back to a full scan instead of seeks
- **Plan quality**: Estimated row counts for OR predicates are often inaccurate

**Solution:** Rewrite as UNION ALL of separate queries (each can use its own index), or create a covering index. In some cases, SQL Server 2019+ can use index intersection, but this is not guaranteed.

## Examples

### Bad

```sql
-- OR on different columns
SELECT * FROM Users
WHERE first_name = @name OR last_name = @name;

-- Different columns in JOIN ON
SELECT * FROM Orders o
JOIN Customers c ON o.billing_id = c.id OR o.shipping_id = c.id;
```

### Good

```sql
-- Rewrite as UNION ALL
SELECT * FROM Users WHERE first_name = @name
UNION ALL
SELECT * FROM Users WHERE last_name = @name AND first_name <> @name;

-- Same column OR is fine
SELECT * FROM Users
WHERE status = 'active' OR status = 'pending';

-- Use IN for same column
SELECT * FROM Users
WHERE status IN ('active', 'pending');
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
    { "id": "avoid-or-on-different-columns", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
