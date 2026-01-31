# Prefer Concat Over Plus

**Rule ID:** `prefer-concat-over-plus`
**Category:** Modernization
**Severity:** Information
**Fixable:** No

## Description

Recommends CONCAT() when + concatenation uses ISNULL/COALESCE; avoids subtle NULL propagation (SQL Server 2012+).

## Rationale

When string concatenation with `+` operator involves `ISNULL()` or `COALESCE()`, using `CONCAT()` function is **clearer and safer**:

1. **NULL propagation with + operator**:
   - Any NULL value in `+` concatenation makes the entire result NULL
   - `'Hello' + NULL + 'World'` = `NULL`
   - Requires wrapping every nullable value in `ISNULL()` or `COALESCE()`

2. **CONCAT() handles NULLs automatically**:
   - `CONCAT('Hello', NULL, 'World')` = `'HelloWorld'`
   - Treats NULL as empty string automatically
   - No need for `ISNULL()` wrappers

3. **Reduced code verbosity**:
   - `ISNULL(a, '') + ISNULL(b, '') + ISNULL(c, '')` (verbose)
   - `CONCAT(a, b, c)` (concise)

4. **Prevents errors**: Easy to forget `ISNULL()` on one nullable column, causing unexpected NULLs

**When to use CONCAT():**
- Concatenating nullable columns
- Building display strings (names, addresses, labels)
- Any scenario where NULLs should be treated as empty strings

**When + is acceptable:**
- All values are guaranteed non-NULL
- NULL propagation is intentional (want NULL result if any input is NULL)

**Compatibility**: `CONCAT()` available in SQL Server 2012+ (compat level 110+).

## Examples

### Bad

```sql
-- Verbose: ISNULL on every nullable column
SELECT ISNULL(@firstName, '') + ' ' + ISNULL(@lastName, '') FROM users;

-- Easy to forget ISNULL on one column (firstName is nullable!)
SELECT @firstName + ' ' + @lastName FROM users;  -- NULL if firstName is NULL

-- Multiple ISNULL calls (hard to read)
SELECT ISNULL(title, '') + ' ' + ISNULL(firstName, '') + ' ' +
       ISNULL(middleName, '') + ' ' + ISNULL(lastName, '') + ' ' +
       ISNULL(suffix, '')
FROM contacts;

-- COALESCE instead of ISNULL (still verbose)
SELECT COALESCE(street, '') + ' ' + COALESCE(city, '') + ', ' +
       COALESCE(state, '') + ' ' + COALESCE(zip, '')
FROM addresses;
```

### Good

```sql
-- CONCAT automatically handles NULLs
SELECT CONCAT(@firstName, ' ', @lastName) FROM users;

-- Multiple fields (much cleaner)
SELECT CONCAT(title, ' ', firstName, ' ', middleName, ' ', lastName, ' ', suffix)
FROM contacts;

-- Address concatenation (readable)
SELECT CONCAT(street, ' ', city, ', ', state, ' ', zip)
FROM addresses;

-- Conditional separators (only add separator if value exists)
SELECT CONCAT(
    firstName,
    CASE WHEN middleName IS NOT NULL THEN ' ' + middleName ELSE '' END,
    ' ',
    lastName
)
FROM contacts;

-- + operator when NULL propagation is desired
SELECT
    order_id,
    customer_name + ' - ' + shipping_address AS full_info
FROM orders
WHERE customer_name + ' - ' + shipping_address IS NOT NULL;  -- Intentional NULL check

-- + operator when all values are non-NULL
SELECT 'Order #' + CAST(order_id AS VARCHAR(10)) FROM orders;  -- order_id is NOT NULL
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
    { "id": "prefer-concat-over-plus", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
