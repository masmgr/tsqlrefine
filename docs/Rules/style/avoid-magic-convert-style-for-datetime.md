# Avoid Magic CONVERT Style for Datetime

**Rule ID:** `avoid-magic-convert-style-for-datetime`
**Category:** Maintainability
**Severity:** Information
**Fixable:** No

## Description

Warns when using numeric style codes (magic numbers) in `CONVERT()` function for datetime formatting. Encourages using `FORMAT()` or explicit date parts for clearer, more maintainable code.

## Rationale

CONVERT style numbers are cryptic and reduce code readability:

**Maintainability issues:**
- **Magic numbers**: Style codes like `101`, `120`, `126` have no obvious meaning
- **Documentation required**: Developers must look up what each number means
- **Error-prone**: Easy to use wrong style code or forget what a number represents
- **Legacy approach**: Modern SQL Server has better alternatives

**Common style numbers:**
- `101` = mm/dd/yyyy (US format)
- `103` = dd/mm/yyyy (European format)
- `120` = yyyy-mm-dd hh:mi:ss (ODBC canonical)
- `126` = ISO 8601 format

**Better alternatives:**
- `FORMAT()` function with clear format strings (SQL Server 2012+)
- `CAST()` or `CONVERT()` without style for ISO dates
- Date part functions like `YEAR()`, `MONTH()`, `DAY()`

## Examples

### Bad

```sql
-- What does 101 mean? Have to look it up
SELECT CONVERT(VARCHAR, GETDATE(), 101);

-- Cryptic style numbers reduce readability
SELECT CONVERT(DATETIME, '2023-01-01', 120);

-- Multiple magic numbers in one query
SELECT
    CONVERT(VARCHAR, start_date, 101) AS start_us,
    CONVERT(VARCHAR, end_date, 103) AS end_eu
FROM events;
```

### Good

```sql
-- FORMAT with clear, self-documenting pattern
SELECT FORMAT(GETDATE(), 'MM/dd/yyyy');

-- ISO 8601 format - unambiguous and standard
SELECT CONVERT(VARCHAR, GETDATE(), 'yyyy-MM-dd');

-- For parsing, use ISO format without style
SELECT CONVERT(DATETIME, '2023-01-01');

-- Explicit date parts when you need specific components
SELECT
    YEAR(order_date) AS order_year,
    MONTH(order_date) AS order_month,
    DAY(order_date) AS order_day
FROM orders;

-- FORMAT with descriptive pattern
SELECT FORMAT(GETDATE(), 'yyyy-MM-dd HH:mm:ss');

-- For display, use FORMAT with culture-aware patterns
SELECT FORMAT(created_date, 'd', 'en-US') FROM users;
```

## Common Style Codes Reference

For reference when refactoring existing code:

| Style | Format | Example |
|-------|--------|---------|
| 101 | mm/dd/yyyy | 12/31/2023 |
| 103 | dd/mm/yyyy | 31/12/2023 |
| 110 | mm-dd-yyyy | 12-31-2023 |
| 111 | yyyy/mm/dd | 2023/12/31 |
| 112 | yyyymmdd | 20231231 |
| 120 | yyyy-mm-dd hh:mi:ss | 2023-12-31 23:59:59 |
| 126 | ISO 8601 | 2023-12-31T23:59:59.000 |

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "avoid-magic-convert-style-for-datetime", "enabled": false }
  ]
}
```

## See Also

- [avoid-ambiguous-datetime-literal](../correctness/avoid-ambiguous-datetime-literal.md) - Disallows slash-delimited date literals
- [utc-datetime](../performance/utc-datetime.md) - Recommends UTC datetime functions
