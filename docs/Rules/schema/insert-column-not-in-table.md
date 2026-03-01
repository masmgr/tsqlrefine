# Insert Column Not In Table

**Rule ID:** `insert-column-not-in-table`
**Category:** Schema
**Severity:** Error
**Fixable:** No

## Description

Detects INSERT statements that reference columns not found in the target table.

## Rationale

Specifying a column in an INSERT column list that does not exist in the target table will cause a runtime error. By validating INSERT column lists against a schema snapshot, you can catch typos and schema drift before deployment.

This rule requires a schema snapshot to be loaded via `--schema` or the `schema.snapshotPath` config option. When no schema is available, the rule is silently skipped.

## Exclusions

The following are not validated:
- **INSERT without a column list** (`INSERT INTO t VALUES (...)`)
- **Temp tables** (`#TempTable`)
- **Table variables** (`@TableVar`)
- **Unresolved tables** (reported by `unresolved-table-reference` instead)

## Examples

### Bad

```sql
-- Column does not exist in target table
INSERT INTO dbo.Users (Id, Name, NonExistentColumn)
VALUES (1, N'Test', N'Bad');

-- Multiple invalid columns
INSERT INTO dbo.Users (BadCol1, BadCol2) VALUES (1, 2);
```

### Good

```sql
-- All columns exist in target table
INSERT INTO dbo.Users (Id, Name, Email)
VALUES (1, N'Test', N'test@example.com');

-- No column list (not validated by this rule)
INSERT INTO dbo.Users VALUES (1, N'Test', N'test@example.com');

-- Temp table (skipped)
INSERT INTO #Temp (Id, Anything) VALUES (1, 2);
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "insert-column-not-in-table", "enabled": false }
  ]
}
```

## See Also

- [update-column-not-in-table](update-column-not-in-table.md)
- [unresolved-table-reference](unresolved-table-reference.md)
- [unresolved-column-reference](unresolved-column-reference.md)
- [TsqlRefine Rules Documentation](../README.md)
