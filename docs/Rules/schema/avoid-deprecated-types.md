# Avoid Deprecated Types

**Rule ID:** `avoid-deprecated-types`
**Category:** Schema
**Severity:** Warning
**Fixable:** No

## Description

Detects deprecated TEXT, NTEXT, and IMAGE data types. Use VARCHAR(MAX), NVARCHAR(MAX), or VARBINARY(MAX) instead.

## Rationale

`TEXT`, `NTEXT`, and `IMAGE` data types have been deprecated since SQL Server 2005. They have several limitations compared to their modern replacements:

- Cannot be used as local variables
- Cannot be used with most string functions
- Limited support in newer features (e.g., columnstore indexes, memory-optimized tables)
- Different API behavior (require special handling with `READTEXT`/`WRITETEXT`/`UPDATETEXT`)

Modern replacements provide the same storage capacity with full T-SQL expression support:

| Deprecated | Replacement |
|-----------|-------------|
| TEXT | VARCHAR(MAX) |
| NTEXT | NVARCHAR(MAX) |
| IMAGE | VARBINARY(MAX) |

## Examples

### Bad

```sql
CREATE TABLE dbo.Documents (
    Content TEXT NOT NULL,
    Notes NTEXT NULL,
    Photo IMAGE NULL
);

DECLARE @notes NTEXT;
```

### Good

```sql
CREATE TABLE dbo.Documents (
    Content VARCHAR(MAX) NOT NULL,
    Notes NVARCHAR(MAX) NULL,
    Photo VARBINARY(MAX) NULL
);

DECLARE @notes NVARCHAR(MAX);
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "avoid-deprecated-types", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
