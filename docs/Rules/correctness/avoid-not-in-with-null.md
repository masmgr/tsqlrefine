# Avoid NOT IN with Subquery

**Rule ID:** `avoid-not-in-with-null`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects NOT IN with subquery which can produce unexpected empty results when the subquery returns NULL values.

## Rationale

`NOT IN` with a subquery is a common source of subtle bugs. When the subquery returns any NULL value, the entire `NOT IN` predicate evaluates to UNKNOWN (not TRUE), causing the query to return zero rows.

This happens because SQL uses three-valued logic:
- `x NOT IN (1, NULL)` is equivalent to `x <> 1 AND x <> NULL`
- `x <> NULL` always evaluates to UNKNOWN
- `TRUE AND UNKNOWN` = UNKNOWN, which is treated as FALSE in WHERE

This behavior is correct per the SQL standard but almost never the intended result.

## Examples

### Bad

```sql
-- If dbo.Blacklist.CustomerId contains any NULL, returns 0 rows
SELECT * FROM dbo.Orders
WHERE CustomerId NOT IN (SELECT CustomerId FROM dbo.Blacklist);
```

### Good

```sql
-- NOT EXISTS is NULL-safe
SELECT * FROM dbo.Orders AS o
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Blacklist AS b WHERE b.CustomerId = o.CustomerId
);

-- EXCEPT is also NULL-safe
SELECT CustomerId FROM dbo.Orders
EXCEPT
SELECT CustomerId FROM dbo.Blacklist;
```

## Schema-Aware Suppression

When a schema snapshot is available (via `--schema` CLI option or configuration), this rule performs additional analysis. If the subquery's SELECT column is provably `NOT NULL` according to the schema, the warning is suppressed because the NULL problem cannot occur.

### Requirements for suppression

All of the following must be true:

- Schema information is loaded
- The subquery has exactly one SELECT column
- The SELECT column is a simple column reference (not an expression)
- The column's table can be resolved in the schema
- The column is defined as `NOT NULL` in the schema

If any condition is not met, the warning is emitted as usual.

### Example

```sql
-- Given: dbo.Blacklist.CustomerId is NOT NULL in the schema
-- This NOT IN is safe and the warning is suppressed
SELECT * FROM dbo.Orders
WHERE CustomerId NOT IN (SELECT CustomerId FROM dbo.Blacklist);

-- Given: dbo.NullableList.CustomerId allows NULL in the schema
-- This NOT IN is still flagged
SELECT * FROM dbo.Orders
WHERE CustomerId NOT IN (SELECT CustomerId FROM dbo.NullableList);
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "avoid-not-in-with-null", "enabled": false }
  ]
}
```

## See Also

- [prefer-exists-over-in-subquery](../performance/prefer-exists-over-in-subquery.md) - Recommends EXISTS over IN with subquery for performance
- [TsqlRefine Rules Documentation](../README.md)
