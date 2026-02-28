# Index Column Not In Table

**Rule ID:** `index-column-not-in-table`
**Category:** Schema
**Severity:** Error
**Fixable:** No

## Description

Detects index definitions that reference columns not found in the target table.

## Rationale

Referencing a non-existent column in a CREATE INDEX statement or an inline index definition within CREATE TABLE will cause a runtime error. This rule validates index column references against the schema snapshot (for CREATE INDEX) or against the table's own column definitions (for inline indexes in CREATE TABLE).

For standalone CREATE INDEX statements, this rule requires a schema snapshot to be loaded via `--schema` or the `schema.snapshotPath` config option. For inline indexes in CREATE TABLE, no schema is needed — columns are validated against the column definitions in the same statement.

## Exclusions

The following are not validated:
- **Temp tables** (`#TempTable`)
- **Table variables** (`@TableVar`)
- **Unresolved tables** (for CREATE INDEX; reported by `unresolved-table-reference` instead)
- **CREATE INDEX without schema** (silently skipped)

## Examples

### Bad

```sql
-- Key column does not exist (CREATE INDEX, requires schema)
CREATE INDEX IX_BadCol ON dbo.Users (BadCol);

-- INCLUDE column does not exist (CREATE INDEX, requires schema)
CREATE INDEX IX_Name ON dbo.Users (Id) INCLUDE (BadCol);

-- Inline index references column not in CREATE TABLE definition
CREATE TABLE dbo.Foo (
    Id INT,
    Name NVARCHAR(50),
    INDEX IX_Bad (NonExistent)
);

-- Inline index INCLUDE references column not in CREATE TABLE definition
CREATE TABLE dbo.Bar (
    Id INT,
    INDEX IX_Name (Id) INCLUDE (MissingCol)
);
```

### Good

```sql
-- Valid CREATE INDEX
CREATE INDEX IX_Name ON dbo.Users (Name);

-- Valid CREATE INDEX with INCLUDE
CREATE INDEX IX_Name ON dbo.Users (Id) INCLUDE (Name, Email);

-- Valid inline index
CREATE TABLE dbo.Baz (
    Id INT,
    Name NVARCHAR(50),
    INDEX IX_Name (Name)
);

-- Temp table indexes are skipped
CREATE INDEX IX_Name ON #Temp (Anything);
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "index-column-not-in-table", "enabled": false }
  ]
}
```

## See Also

- [duplicate-index-column](duplicate-index-column.md)
- [duplicate-index-definition](duplicate-index-definition.md)
- [unresolved-table-reference](unresolved-table-reference.md)
- [TsqlRefine Rules Documentation](../README.md)
