# Prefer Concat With Nullable

**Rule ID:** `prefer-concat-with-nullable`
**Category:** Modernization
**Severity:** Information
**Fixable:** No

## Description

Stricter variant that also detects CAST/CONVERT in concatenations; enable instead of prefer-concat-over-plus for comprehensive coverage (SQL Server 2012+).

## Rationale

This is a **stricter variant** of `prefer-concat-over-plus` that also detects `CAST()`/`CONVERT()` in string concatenations:

1. **CAST/CONVERT with + operator creates double complexity**:
   - Need to convert non-string types to strings
   - Need to handle NULLs with `ISNULL()` or `COALESCE()`
   - Results in verbose, hard-to-read expressions

2. **CONCAT() handles both concerns automatically**:
   - Automatically converts all arguments to strings (no CAST/CONVERT needed)
   - Treats NULL as empty string (no ISNULL/COALESCE needed)
   - Single function call instead of nested functions

3. **Common pattern**: Concatenating numbers, dates, or other non-string types with strings
   - `'Order #' + CAST(order_id AS VARCHAR)` requires explicit cast
   - `CONCAT('Order #', order_id)` works directly

**Enable this rule instead of `prefer-concat-over-plus`** for more comprehensive coverage.

**Compatibility**: `CONCAT()` available in SQL Server 2012+ (compat level 110+).

## Examples

### Bad

```sql
-- ISNULL + nullable columns (verbose)
SELECT ISNULL(first_name, '') + ' ' + ISNULL(last_name, '') AS full_name FROM users;

-- CAST + concatenation (verbose)
SELECT 'Order #' + CAST(order_id AS VARCHAR(10)) FROM orders;

-- CONVERT + concatenation
SELECT 'Date: ' + CONVERT(VARCHAR, order_date, 120) FROM orders;

-- ISNULL + CAST + concatenation (very verbose!)
SELECT ISNULL('User ', '') + CAST(user_id AS VARCHAR(10)) + ': ' + ISNULL(username, 'N/A')
FROM users;

-- Multiple CAST with NULL handling
SELECT COALESCE('Qty: ' + CAST(quantity AS VARCHAR), 'N/A') + ' - ' +
       COALESCE('Price: $' + CAST(price AS VARCHAR), 'N/A')
FROM products;
```

### Good

```sql
-- CONCAT handles nullable columns automatically
SELECT CONCAT(first_name, ' ', last_name) AS full_name FROM users;

-- CONCAT auto-converts numbers to strings
SELECT CONCAT('Order #', order_id) FROM orders;

-- CONCAT with date conversion (use FORMAT or CONVERT separately if specific format needed)
SELECT CONCAT('Date: ', order_date) FROM orders;
-- Or with specific format:
SELECT CONCAT('Date: ', FORMAT(order_date, 'yyyy-MM-dd HH:mm:ss')) FROM orders;

-- CONCAT simplifies complex expressions
SELECT CONCAT('User ', user_id, ': ', username)
FROM users;

-- CONCAT with multiple data types
SELECT CONCAT('Qty: ', quantity, ' - Price: $', price)
FROM products;

-- Complex example: Build full address
SELECT CONCAT(
    street_number, ' ',
    street_name, ', ',
    city, ', ',
    state, ' ',
    zip_code
) AS full_address
FROM addresses;

-- CONCAT with CASE for conditional formatting
SELECT CONCAT(
    'Order #', order_id,
    ' - Status: ', status,
    CASE WHEN shipped_date IS NOT NULL
         THEN CONCAT(' (Shipped: ', FORMAT(shipped_date, 'yyyy-MM-dd'), ')')
         ELSE '' END
) AS order_info
FROM orders;
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
    { "id": "prefer-concat-with-nullable", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
