# Unresolved Column Reference

**Rule ID:** `unresolved-column-reference`
**Category:** Schema
**Severity:** Warning
**Fixable:** No

## Description

Detects references to columns that do not exist in the schema snapshot.

## Rationale

Referencing a column that does not exist in a table will cause a runtime error. Additionally, unqualified column references that match columns in multiple tables create ambiguous queries that may break when the schema changes. By validating column references against a schema snapshot, you can catch these issues before deployment.

This rule requires a schema snapshot to be loaded via `--schema` or the `schema.snapshotPath` config option. When no schema is available, the rule is silently skipped.

## Detection Modes

### Qualified columns (`alias.column`)

When a column is qualified with a table alias or name, the rule resolves the alias to a table and checks whether the column exists in that table.

### Unqualified columns

When a column has no table qualifier, the rule searches all tables in the current FROM scope:
- **0 matches**: Reports the column as not found
- **1 match**: No diagnostic (column is unambiguous)
- **2+ matches**: Reports an ambiguous column reference

## Exclusions

The following references are not validated:
- **Wildcard** (`SELECT *`)
- **Columns on unresolvable aliases** (CTEs, derived tables, temp tables)
- **Columns on unresolved tables** (reported by `unresolved-table-reference` instead)

## Examples

### Bad

```sql
-- Qualified column does not exist
SELECT u.NonExistentColumn FROM dbo.Users AS u;

-- Unqualified column not found in any table
SELECT MissingCol FROM dbo.Users;

-- Ambiguous column reference (Id exists in both tables)
SELECT Id
FROM dbo.Users AS u
INNER JOIN dbo.Orders AS o ON u.Id = o.Id;
```

### Good

```sql
-- Qualified column exists
SELECT u.Id, u.Name FROM dbo.Users AS u;

-- Unqualified column is unique across tables in scope
SELECT Total
FROM dbo.Users AS u
INNER JOIN dbo.Orders AS o ON u.Id = o.UserId;

-- Wildcard is not validated
SELECT * FROM dbo.Users;

-- Derived table columns are skipped
SELECT d.Col FROM (SELECT 1 AS Col) AS d;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "unresolved-column-reference", "enabled": false }
  ]
}
```

## See Also

- [unresolved-table-reference](unresolved-table-reference.md)
- [insert-column-not-in-table](insert-column-not-in-table.md)
- [update-column-not-in-table](update-column-not-in-table.md)
- [TsqlRefine Rules Documentation](../README.md)
