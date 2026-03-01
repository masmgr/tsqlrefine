# Unresolved Table Reference

**Rule ID:** `unresolved-table-reference`
**Category:** Schema
**Severity:** Warning
**Fixable:** No

## Description

Detects references to tables or views that do not exist in the schema snapshot.

## Rationale

Referencing a table or view that does not exist in the database will cause a runtime error. By validating table references against a schema snapshot at lint time, you can catch typos, missing migrations, and stale references before deployment.

This rule requires a schema snapshot to be loaded via `--schema` or the `schema.snapshotPath` config option. When no schema is available, the rule is silently skipped.

## Exclusions

The following references are not validated:
- **Temp tables** (`#TempTable`, `##GlobalTemp`)
- **Table variables** (`@TableVar`)
- **System schemas** (`sys.*`, `INFORMATION_SCHEMA.*`)

## Examples

### Bad

```sql
-- Table does not exist in schema snapshot
SELECT * FROM dbo.NonExistentTable;

-- Missing table in JOIN
SELECT u.Id
FROM dbo.Users AS u
INNER JOIN dbo.MissingTable AS m ON u.Id = m.UserId;
```

### Good

```sql
-- Table exists in schema snapshot
SELECT * FROM dbo.Users;

-- Temp tables are skipped
SELECT * FROM #TempTable;

-- System schemas are skipped
SELECT * FROM sys.objects;
SELECT * FROM INFORMATION_SCHEMA.TABLES;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "unresolved-table-reference", "enabled": false }
  ]
}
```

## See Also

- [unresolved-column-reference](unresolved-column-reference.md)
- [insert-column-not-in-table](insert-column-not-in-table.md)
- [update-column-not-in-table](update-column-not-in-table.md)
- [TsqlRefine Rules Documentation](../README.md)
