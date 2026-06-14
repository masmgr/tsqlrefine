# Update Column Not In Table

**Rule ID:** `update-column-not-in-table`
**Category:** Schema
**Severity:** Error
**Fixable:** No

## Description

Detects UPDATE statements that reference columns not found in the target table.

## Rationale

Setting a column in an UPDATE SET clause that does not exist in the target table will cause a runtime error. By validating UPDATE column references against a schema snapshot, you can catch typos and schema drift before deployment.

This rule requires a schema snapshot to be loaded via `--schema` or the `schema.snapshotPath` config option. When no schema is available, the rule is silently skipped.

## Exclusions

The following are not validated:
- **Temp tables** (`#TempTable`)
- **Table variables** (`@TableVar`)
- **Unresolved tables** (reported by `unresolved-table-reference` instead)

## Examples

### Bad

```sql
-- Column does not exist in target table
UPDATE dbo.Users SET NonExistentColumn = 1 WHERE Id = 1;

-- Multiple invalid columns
UPDATE dbo.Users SET Bad1 = 1, Bad2 = 2 WHERE Id = 1;
```

### Good

```sql
-- All columns exist in target table
UPDATE dbo.Users
SET Name = N'Updated', Email = N'new@example.com'
WHERE Id = 1;

-- Temp table (skipped)
UPDATE #Temp SET Anything = 1;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "update-column-not-in-table", "enabled": false }
  ]
}
```

## See Also

- [insert-column-not-in-table](insert-column-not-in-table.md)
- [unresolved-table-reference](unresolved-table-reference.md)
- [unresolved-column-reference](unresolved-column-reference.md)
- [TsqlRefine Rules Documentation](../README.md)
