# Avoid Scalar UDF In Query

**Rule ID:** `avoid-scalar-udf-in-query`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Detects user-defined scalar function calls in queries which execute row-by-row and cause severe performance degradation. Schema-qualified function calls (e.g., `dbo.MyFunc()`) are flagged; built-in functions are excluded.

## Rationale

Scalar user-defined functions (UDFs) in SQL Server execute row-by-row, preventing parallelism and causing severe performance problems:

**Performance problems:**
- **Row-by-row execution**: The function is called once per row, even for millions of rows
- **Parallelism disabled**: SQL Server disables parallel execution plans when scalar UDFs are present
- **Hidden I/O**: UDFs may contain data access that is invisible to the optimizer
- **Poor cardinality estimates**: The optimizer cannot accurately estimate costs through UDF calls

**Solution:** Replace scalar UDFs with inline table-valued functions (iTVFs) via CROSS APPLY, computed columns, or rewrite the logic inline in the query.

## Examples

### Bad

```sql
-- Scalar UDF in SELECT list
SELECT dbo.FormatName(first_name, last_name) AS full_name
FROM Employees;

-- Scalar UDF in WHERE clause
SELECT *
FROM Orders
WHERE dbo.CalculateDiscount(order_id) > 100;

-- Scalar UDF in JOIN ON
SELECT *
FROM Orders o
JOIN Customers c ON dbo.NormalizeCode(o.customer_code) = c.code;

-- Multiple scalar UDFs
SELECT dbo.Fn1(col1), dbo.Fn2(col2)
FROM LargeTable;
```

### Good

```sql
-- Inline the logic directly
SELECT first_name + ' ' + last_name AS full_name
FROM Employees;

-- Use inline table-valued function with CROSS APPLY
SELECT o.*, d.discount_amount
FROM Orders o
CROSS APPLY dbo.CalculateDiscountTvf(o.order_id) d;

-- Built-in functions are fine
SELECT UPPER(name), GETDATE()
FROM Products;
```

## Configuration

To disable this rule, add it to your `tsqlrefine.json`:

```json
{
  "ruleset": "custom-ruleset.json"
}
```

In `custom-ruleset.json`:

```json
{
  "rules": [
    { "id": "avoid-scalar-udf-in-query", "enabled": false }
  ]
}
```

## See Also

- [non-sargable](non-sargable.md) - Detects functions on columns in predicates
- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
