# Order By In Subquery

**Rule ID:** `order-by-in-subquery`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Disallows invalid ORDER BY in subqueries unless paired with TOP, OFFSET, FOR XML, or FOR JSON (SQL Server error Msg 1033).

## Rationale

In SQL Server, `ORDER BY` is only valid at the outermost query level unless it is paired with an operator that makes row ordering meaningful for the subquery itself (e.g., `TOP`, `OFFSET/FETCH`, `FOR XML`, `FOR JSON`).

Without one of these, SQL Server raises error Msg 1033. This rule catches the issue early and nudges you toward moving the ordering to the correct level.

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
