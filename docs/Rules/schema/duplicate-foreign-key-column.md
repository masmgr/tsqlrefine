# Duplicate Foreign Key Column

**Rule ID:** `duplicate-foreign-key-column`
**Category:** Schema
**Severity:** Warning
**Fixable:** No

## Description

Detects duplicate columns within a single FOREIGN KEY constraint definition.

## Rationale

Specifying the same column more than once in a FOREIGN KEY constraint's column list is redundant and almost certainly a mistake. It typically results from a copy-paste error when defining composite foreign keys. SQL Server may reject the statement or produce unexpected behavior depending on the context.

Column name comparison is case-insensitive.

## Examples

### Bad

```sql
-- Column 'a' appears twice in FOREIGN KEY
CREATE TABLE dbo.Example1 (
    a INT,
    b INT,
    FOREIGN KEY (a, b, a) REFERENCES dbo.Other (x, y, z)
);

-- Named constraint with duplicate column
CREATE TABLE dbo.Example2 (
    col1 INT,
    col2 INT,
    CONSTRAINT FK_Example2 FOREIGN KEY (col1, col1) REFERENCES dbo.Other (x, y)
);
```

### Good

```sql
-- All columns are unique in FOREIGN KEY
CREATE TABLE dbo.Example3 (
    a INT,
    b INT,
    FOREIGN KEY (a, b) REFERENCES dbo.Other (x, y)
);
```

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "duplicate-foreign-key-column", "enabled": false }
  ]
}
```

## See Also

- [duplicate-column-definition](duplicate-column-definition.md) - Detects duplicate column names in table definitions
- [duplicate-index-column](duplicate-index-column.md) - Detects duplicate columns within an index definition
