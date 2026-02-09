# Duplicate Index Definition

**Rule ID:** `duplicate-index-definition`
**Category:** Schema
**Severity:** Warning
**Fixable:** No

## Description

Detects multiple indexes or unique constraints within a table that have the exact same column composition (same columns, same order, same sort direction).

## Rationale

Having two or more indexes with identical column compositions wastes storage, slows down write operations (INSERT/UPDATE/DELETE), and increases maintenance overhead — all without providing any query performance benefit. This is almost always a mistake, such as:

- A copy-paste error when adding a new index
- Forgetting that a PRIMARY KEY or UNIQUE constraint already creates an implicit index
- An INDEX definition that duplicates a separately declared UNIQUE constraint

Duplicate indexes should be consolidated into a single index.

## Examples

### Bad

```sql
-- IX_1 and IX_2 have identical column composition
CREATE TABLE dbo.Example1 (
    a INT,
    b INT,
    INDEX IX_1 (a, b),
    INDEX IX_2 (a, b)
);

-- Index duplicates UNIQUE constraint
CREATE TABLE dbo.Example2 (
    a INT,
    b INT,
    INDEX IX_1 (a, b),
    CONSTRAINT UQ_1 UNIQUE (a, b)
);

-- Index duplicates PRIMARY KEY
CREATE TABLE dbo.Example3 (
    a INT,
    CONSTRAINT PK_Example3 PRIMARY KEY (a),
    INDEX IX_1 (a)
);
```

### Good

```sql
-- Different column order — not considered duplicates
CREATE TABLE dbo.Example4 (
    a INT,
    b INT,
    INDEX IX_1 (a, b),
    INDEX IX_2 (b, a)
);

-- Different sort order — not considered duplicates
CREATE TABLE dbo.Example5 (
    a INT,
    INDEX IX_1 (a ASC),
    INDEX IX_2 (a DESC)
);

-- Different column sets
CREATE TABLE dbo.Example6 (
    a INT,
    b INT,
    c INT,
    INDEX IX_1 (a, b),
    INDEX IX_2 (a, c)
);
```

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "duplicate-index-definition", "enabled": false }
  ]
}
```

## See Also

- [duplicate-index-column](duplicate-index-column.md) - Detects duplicate columns within a single index definition
- [avoid-heap-table](avoid-heap-table.md) - Warns when tables are created without a clustered index
