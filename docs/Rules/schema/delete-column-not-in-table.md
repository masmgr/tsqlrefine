# Delete Column Not In Table

**Rule ID:** `delete-column-not-in-table`
**Category:** Schema
**Severity:** Error
**Fixable:** No

## Description

Detects DELETE statements whose WHERE clause references columns not found in the target table.

## Rationale

Referencing a column in a DELETE WHERE clause that does not exist in the target table (or any joined table in a multi-table DELETE) will cause a runtime error. By validating DELETE column references against a schema snapshot, you can catch typos and schema drift before deployment.

This rule requires a schema snapshot to be loaded via `--schema` or the `schema.snapshotPath` config option. When no schema is available, the rule is silently skipped.

## Exclusions

The following are not validated:
- **DELETE without WHERE** (nothing to validate)
- **Temp tables** (`#TempTable`)
- **Table variables** (`@TableVar`)
- **Unresolved tables** (reported by `unresolved-table-reference` instead)
- **Subquery columns** (subqueries in WHERE have their own scope)

## Examples

### Bad

```sql
-- Column does not exist in target table
DELETE FROM dbo.Users WHERE BadCol = 1;

-- Qualified column does not exist
DELETE u FROM dbo.Users AS u WHERE u.NonExistent = 1;

-- Column does not exist in joined table
DELETE u
FROM dbo.Users AS u
INNER JOIN dbo.Orders AS o ON u.Id = o.UserId
WHERE o.BadCol = 1;
```

### Good

```sql
-- All columns exist in target table
DELETE FROM dbo.Users WHERE Id = 1;

-- Valid multi-table DELETE
DELETE u
FROM dbo.Users AS u
INNER JOIN dbo.Orders AS o ON u.Id = o.UserId
WHERE o.Total > 100;

-- DELETE without WHERE (skipped)
DELETE FROM dbo.Users;

-- Temp table (skipped)
DELETE FROM #Temp WHERE Anything = 1;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "delete-column-not-in-table", "enabled": false }
  ]
}
```

## See Also

- [insert-column-not-in-table](insert-column-not-in-table.md)
- [update-column-not-in-table](update-column-not-in-table.md)
- [unresolved-table-reference](unresolved-table-reference.md)
- [unresolved-column-reference](unresolved-column-reference.md)
- [TsqlRefine Rules Documentation](../README.md)
