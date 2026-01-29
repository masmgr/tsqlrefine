# Order By In Subquery

**Rule ID:** `order-by-in-subquery`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects ORDER BY in subqueries without TOP, OFFSET, FOR XML, or FOR JSON, which is wasteful as the optimizer may ignore it.

## Rationale

ORDER BY in subqueries without TOP/OFFSET/FOR XML/FOR JSON is problematic because:

1. **Optimizer ignores it**: SQL Server optimizer may discard the ORDER BY clause in subqueries, making it wasteful
2. **SQL Server error in some contexts**: Without TOP/OFFSET/FOR XML/FOR JSON, SQL Server raises error Msg 1033 in derived tables and some subquery contexts
3. **Performance overhead**: Unnecessary sorting consumes CPU and memory
4. **Misleading code**: Suggests ordering affects outer query, but it doesn't

**Why ORDER BY is ignored**:

SQL has no concept of "ordered set" from subqueries. The relational model treats subquery results as unordered sets. Only the outermost ORDER BY determines final result order.

**Error Msg 1033**:
```
The ORDER BY clause is invalid in views, inline functions, derived tables,
subqueries, and common table expressions, unless TOP, OFFSET or FOR XML is also specified.
```

This error occurs in:
- Derived tables (FROM subquery)
- Common Table Expressions (CTEs)
- Views
- Inline table-valued functions

**When ORDER BY in subquery IS valid**:
- With TOP: `SELECT TOP 10 ... ORDER BY ...` (ordering determines which 10 rows)
- With OFFSET/FETCH: `... ORDER BY ... OFFSET 0 ROWS` (paging requires ordering)
- With FOR XML: `... FOR XML PATH('...') ORDER BY ...` (XML order matters)
- With FOR JSON: `... FOR JSON PATH ORDER BY ...` (JSON order matters)

## Examples

### Bad

```sql
-- ORDER BY inside a derived table (SQL Server error Msg 1033)
SELECT *
FROM (
  SELECT id, name
  FROM users
  ORDER BY name
) u;

-- ORDER BY inside a scalar subquery (also invalid without an exception)
SELECT
  (SELECT name FROM users ORDER BY name) AS first_name;
```

### Good

```sql
-- Move ORDER BY to the outer query
SELECT u.id, u.name
FROM (
  SELECT id, name
  FROM users
) u
ORDER BY u.name;

-- Valid exception: ORDER BY paired with TOP
SELECT *
FROM (
  SELECT TOP (10) id, name
  FROM users
  ORDER BY name
) u;
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
    { "id": "order-by-in-subquery", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Documentation](../../configuration.md)
