# Avoid FLOAT for Decimal Values

**Rule ID:** `avoid-float-for-decimal`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects FLOAT/REAL data types which have binary rounding issues. Use DECIMAL/NUMERIC for exact precision.

## Rationale

`FLOAT` and `REAL` are IEEE 754 floating-point types that use binary representation internally. This means they cannot represent many decimal fractions exactly, leading to rounding errors in arithmetic operations.

For example, `0.1 + 0.2` in floating-point arithmetic does not equal `0.3` exactly. This is particularly problematic for:

- **Financial calculations**: Monetary amounts must be exact
- **Aggregations**: Rounding errors accumulate over many rows
- **Equality comparisons**: `WHERE price = 19.99` may not match expected rows

Use `DECIMAL(p,s)` or `NUMERIC(p,s)` for values requiring exact precision. Use `MONEY` or `SMALLMONEY` for currency values.

## Examples

### Bad

```sql
-- FLOAT columns have rounding issues
CREATE TABLE dbo.Products (
    Price FLOAT NOT NULL,
    Cost REAL NOT NULL
);

-- FLOAT variables
DECLARE @amount FLOAT;
SET @amount = 0.1 + 0.2; -- Not exactly 0.3

-- FLOAT parameters
CREATE PROCEDURE dbo.UpdatePrice
    @price FLOAT
AS
    SELECT @price;
```

### Good

```sql
-- DECIMAL provides exact precision
CREATE TABLE dbo.Products (
    Price DECIMAL(18, 2) NOT NULL,
    Cost NUMERIC(10, 4) NOT NULL
);

-- MONEY for currency
CREATE TABLE dbo.Orders (
    Total MONEY NOT NULL,
    Tax SMALLMONEY NOT NULL
);

-- DECIMAL variables
DECLARE @amount DECIMAL(18, 2);
SET @amount = 0.1 + 0.2; -- Exactly 0.3
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "avoid-float-for-decimal", "enabled": false }
  ]
}
```

## See Also

- [semantic/data-type-length](semantic-data-type-length.md) - Requires explicit length for variable-length types
- [TsqlRefine Rules Documentation](../README.md)
