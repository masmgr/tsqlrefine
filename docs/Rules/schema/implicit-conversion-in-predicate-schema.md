# Implicit Conversion in Predicate (Schema)

**Rule ID:** `implicit-conversion-in-predicate-schema`
**Category:** Schema
**Severity:** Warning
**Fixable:** No

## Description

Detects implicit type conversions on columns in predicates using schema type information.

## Rationale

When SQL Server compares values of different types in a predicate (WHERE, JOIN ON, HAVING), it implicitly converts one side to match the other based on type precedence rules. If the **column** side is the one being converted, the query optimizer cannot use indexes on that column, resulting in full table or index scans. This rule uses actual schema type information to precisely detect when a column undergoes implicit conversion, unlike the syntax-only `avoid-implicit-conversion-in-predicate` rule which can only detect simple patterns.

Common problematic patterns:
- `WHERE varchar_column = N'text'` — varchar column is converted to nvarchar
- `WHERE varchar_column = 1` — varchar column is converted to int
- `JOIN ... ON int_column = decimal_column` — int column is converted to decimal

This rule only warns when the **column side** is converted. Literal-side conversions (e.g., `WHERE int_column = '1'`) are harmless and not flagged.

## Examples

### Bad

```sql
-- varchar column compared with nvarchar literal (column is converted)
SELECT Email FROM dbo.Users AS u WHERE u.Email = N'test@example.com';

-- varchar column compared with int literal (column is converted)
SELECT Email FROM dbo.Users AS u WHERE u.Email = 1;

-- datetime column joined with datetime2 column (datetime is converted)
SELECT u.Id
FROM dbo.Users AS u
INNER JOIN dbo.Orders AS o ON u.CreatedAt = o.OrderDate;
```

### Good

```sql
-- Same-type comparison (no conversion)
SELECT Id FROM dbo.Users AS u WHERE u.Id = 1;

-- nvarchar column with nvarchar literal (same category)
SELECT Name FROM dbo.Users AS u WHERE u.Name = N'Test';

-- Literal-side conversion only (int column vs varchar literal — literal is converted)
SELECT Id FROM dbo.Users AS u WHERE u.Id = '1';

-- varchar column with varchar literal (same type)
SELECT Email FROM dbo.Users AS u WHERE u.Email = 'test';
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "implicit-conversion-in-predicate-schema", "enabled": false }
  ]
}
```

## See Also

- [avoid-implicit-conversion-in-predicate](../performance/avoid-implicit-conversion-in-predicate.md) — Syntax-based version (no schema required)
- [TsqlRefine Rules Documentation](../README.md)
