# Prefer Try Convert Patterns

**Rule ID:** `prefer-try-convert-patterns`
**Category:** Modernization
**Severity:** Information
**Fixable:** No

## Description

Recommends TRY_CONVERT/TRY_CAST over CASE + ISNUMERIC/ISDATE; fewer false positives and clearer intent.

## Rationale

Using `CASE WHEN ISNUMERIC(...) = 1 THEN CONVERT(...)` or `CASE WHEN ISDATE(...) = 1 THEN CONVERT(...)` is **verbose and has false positives** compared to `TRY_CONVERT()`/`TRY_CAST()`:

1. **ISNUMERIC() has false positives**:
   - Returns 1 for values that are **not** valid integers (e.g., `'$123'`, `'1e5'`, `','`, `'+'`, `'-'`)
   - `ISNUMERIC(',') = 1` but `CONVERT(INT, ',')` throws error
   - Cannot distinguish between INT, FLOAT, DECIMAL, MONEY

2. **ISDATE() has limitations**:
   - Depends on current language/locale settings (not deterministic)
   - Accepts ambiguous date strings that may convert incorrectly
   - `ISDATE('13/01/2024')` may return 0 or 1 depending on locale

3. **Verbose CASE logic**:
   - `CASE WHEN ISNUMERIC(@value) = 1 THEN CONVERT(INT, @value) ELSE NULL END` (64 characters)
   - `TRY_CONVERT(INT, @value)` (26 characters)

4. **TRY_CONVERT() is safer and clearer**:
   - Returns NULL on conversion failure (no error thrown)
   - Works for all data types, not just numbers/dates
   - Intent is immediately clear
   - Handles edge cases correctly

**Compatibility**: `TRY_CONVERT()` and `TRY_CAST()` available in SQL Server 2012+ (compat level 110+).

## Examples

### Bad

```sql
-- ISNUMERIC with CASE (verbose and has false positives)
SELECT CASE WHEN ISNUMERIC(@value) = 1
            THEN CONVERT(INT, @value)
            ELSE NULL END;

-- ISNUMERIC false positive: '$123' is numeric but not INT
DECLARE @x VARCHAR(10) = '$123';
SELECT CASE WHEN ISNUMERIC(@x) = 1  -- Returns 1 (true)
            THEN CONVERT(INT, @x)    -- Throws error!
            ELSE NULL END;

-- ISDATE with CASE (locale-dependent)
SELECT CASE WHEN ISDATE(@dateString) = 1
            THEN CONVERT(DATE, @dateString)
            ELSE NULL END;

-- Multiple CASE statements (very verbose)
SELECT
    CASE WHEN ISNUMERIC(quantity) = 1 THEN CAST(quantity AS INT) ELSE 0 END,
    CASE WHEN ISNUMERIC(price) = 1 THEN CAST(price AS DECIMAL(10,2)) ELSE 0 END,
    CASE WHEN ISDATE(order_date) = 1 THEN CAST(order_date AS DATE) ELSE NULL END
FROM orders;

-- ISNUMERIC doesn't distinguish types
SELECT CASE WHEN ISNUMERIC('1e5') = 1  -- Returns 1
            THEN CONVERT(INT, '1e5')   -- Error: '1e5' is not INT
            ELSE NULL END;
```

### Good

```sql
-- TRY_CONVERT (clean and safe)
SELECT TRY_CONVERT(INT, @value);

-- Handles false positives correctly
DECLARE @x VARCHAR(10) = '$123';
SELECT TRY_CONVERT(INT, @x);  -- Returns NULL (correct)

-- TRY_CONVERT with dates (locale-independent)
SELECT TRY_CONVERT(DATE, @dateString);

-- Multiple conversions (concise)
SELECT
    TRY_CAST(quantity AS INT),
    TRY_CAST(price AS DECIMAL(10,2)),
    TRY_CAST(order_date AS DATE)
FROM orders;

-- TRY_CONVERT with style (for date formats)
SELECT TRY_CONVERT(DATE, '2024-01-31', 120) AS iso_date;

-- TRY_CAST (alternative to TRY_CONVERT)
SELECT TRY_CAST(@value AS INT);

-- With COALESCE for default values
SELECT COALESCE(TRY_CONVERT(INT, quantity_string), 0) AS quantity
FROM orders;

-- Complex example: Multiple conversions with error handling
SELECT
    order_id,
    TRY_CONVERT(INT, quantity) AS quantity,
    TRY_CONVERT(DECIMAL(10,2), price) AS price,
    TRY_CONVERT(DATE, order_date, 103) AS order_date,  -- dd/mm/yyyy format
    CASE
        WHEN TRY_CONVERT(INT, quantity) IS NULL THEN 'Invalid quantity'
        WHEN TRY_CONVERT(DECIMAL(10,2), price) IS NULL THEN 'Invalid price'
        ELSE 'OK'
    END AS validation_status
FROM staging_orders;

-- When you need specific error messages (combine with ISNULL check)
SELECT
    TRY_CONVERT(INT, @value) AS converted_value,
    CASE
        WHEN @value IS NULL THEN 'NULL input'
        WHEN TRY_CONVERT(INT, @value) IS NULL THEN 'Invalid format'
        ELSE 'Success'
    END AS status;
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
    { "id": "prefer-try-convert-patterns", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
