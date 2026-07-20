# Avoid Deprecated Types

**Rule ID:** `avoid-deprecated-types`
**Category:** Schema
**Severity:** Warning
**Fixable:** No

## Description

Detects deprecated TEXT, NTEXT, IMAGE, and TIMESTAMP data types and recommends modern replacements.

## Rationale

`TEXT`, `NTEXT`, and `IMAGE` data types have been deprecated since SQL Server 2005. `TIMESTAMP` is a deprecated synonym for `ROWVERSION` and does not represent a date or time. These types have limitations or misleading names compared to their modern replacements:

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
| TIMESTAMP | ROWVERSION |

## Examples

### Bad

```sql
CREATE TABLE dbo.Documents (
    Content TEXT NOT NULL,
    Notes NTEXT NULL,
    Photo IMAGE NULL,
    Version TIMESTAMP NOT NULL
);

DECLARE @notes NTEXT;
```

### Good

```sql
CREATE TABLE dbo.Documents (
    Content VARCHAR(MAX) NOT NULL,
    Notes NVARCHAR(MAX) NULL,
    Photo VARBINARY(MAX) NULL,
    Version ROWVERSION NOT NULL
);

DECLARE @notes NVARCHAR(MAX);
```

## Configuration

To disable this rule:

```json
{
  "rules": {
    "avoid-deprecated-types": "none"
  }
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
