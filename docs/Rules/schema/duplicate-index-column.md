# Duplicate Index Column

**Rule ID:** `duplicate-index-column`
**Category:** Schema
**Severity:** Warning
**Fixable:** No

## Description

Detects duplicate columns within a single index, PRIMARY KEY, or UNIQUE constraint definition.

## Rationale

Specifying the same column more than once in a single index or constraint definition is redundant and likely a copy-paste error. SQL Server may accept the statement depending on context, but the duplicate column provides no benefit and can indicate a logic mistake in the schema design.

This rule checks:
- Inline `INDEX` definitions
- Table-level `PRIMARY KEY` constraints
- Table-level `UNIQUE` constraints

Column name comparison is case-insensitive.

## Examples

### Bad

```sql
-- Column 'a' appears twice in index
CREATE TABLE dbo.Example1 (
    a INT,
    b INT,
    INDEX IX_1 (a, b, a)
);

-- Duplicate column in PRIMARY KEY
CREATE TABLE dbo.Example2 (
    a INT,
    b INT,
    CONSTRAINT PK_Example2 PRIMARY KEY (a, b, a)
);

-- Duplicate column in UNIQUE constraint
CREATE TABLE dbo.Example3 (
    x INT,
    y INT,
    CONSTRAINT UQ_Example3 UNIQUE (x, y, x)
);
```

### Good

```sql
-- All columns are unique within each index/constraint
CREATE TABLE dbo.Example (
    a INT,
    b INT,
    c INT,
    INDEX IX_1 (a, b),
    CONSTRAINT PK_Example PRIMARY KEY (c)
);
```

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "duplicate-index-column", "enabled": false }
  ]
}
```

## See Also

- [duplicate-index-definition](duplicate-index-definition.md) - Detects multiple indexes with the same column composition
- [duplicate-column-definition](duplicate-column-definition.md) - Detects duplicate column names in table definitions
