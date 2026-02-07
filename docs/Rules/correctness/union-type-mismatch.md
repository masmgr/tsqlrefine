# Union Type Mismatch

**Rule ID:** `union-type-mismatch`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects UNION/UNION ALL where corresponding columns have obviously different literal types, which may cause implicit conversion or data truncation.

## Rationale

When UNION or UNION ALL combines queries with different column types, SQL Server performs implicit type conversion according to data type precedence rules. This can lead to unexpected data truncation (e.g., numeric values converted to strings), conversion errors at runtime, or silent data loss. By detecting obvious type mismatches between literal values and CAST/CONVERT expressions, this rule helps catch common copy-paste mistakes and logic errors early.

The rule only flags mismatches that can be determined statically from literal types (numeric vs string, etc.) and CAST/CONVERT target types. Column references and complex expressions are not flagged since their types cannot be resolved without schema information.

## Examples

### Bad

```sql
-- Numeric vs String mismatch
SELECT 1 AS Id
UNION ALL
SELECT 'text' AS Id;

-- Multiple column mismatches
SELECT 1, 'hello'
UNION ALL
SELECT 'a', 2;

-- CAST type mismatch
SELECT CAST(1 AS INT)
UNION ALL
SELECT CAST('a' AS VARCHAR(10));
```

### Good

```sql
-- Same types
SELECT 1 AS Id
UNION ALL
SELECT 2 AS Id;

-- Same string types
SELECT 'hello' AS Val
UNION ALL
SELECT 'world' AS Val;

-- NULL is compatible with any type
SELECT 1 AS Id
UNION ALL
SELECT NULL AS Id;

-- Column references (types not determinable statically)
SELECT Name FROM Users
UNION ALL
SELECT Title FROM Products;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "union-type-mismatch", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
