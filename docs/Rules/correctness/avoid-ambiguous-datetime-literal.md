# Avoid Ambiguous Datetime Literal

**Rule ID:** `avoid-ambiguous-datetime-literal`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Disallows slash-delimited date literals (e.g., `'12/31/2023'`, `'31/12/2023'`) as they depend on language and locale settings and can silently change meaning.

## Rationale

Slash-delimited date literals are ambiguous because their interpretation depends on the SQL Server's language and locale settings:

- **Locale dependency**: `'12/31/2023'` might be interpreted as December 31st in US locale but fail or be misinterpreted in European locales where day comes first
- **Silent errors**: The same date string can be interpreted differently in different environments without warning
- **Maintenance issues**: Moving databases between servers or changing locale settings can cause data corruption
- **Testing problems**: Queries that work in development may fail or produce wrong results in production

The ISO 8601 format (`'YYYY-MM-DD'` or `'YYYYMMDD'`) is unambiguous and works consistently across all locales and language settings.

## Examples

### Bad

```sql
-- US format - ambiguous
SELECT * FROM orders WHERE order_date = '12/31/2023';

-- Could be March 15 or December 3, depending on locale
SELECT * FROM users WHERE created_at > '3/15/2024';

-- European format - also ambiguous
SELECT * FROM events WHERE event_date = '31/12/2023';

-- Two-digit year - even more ambiguous
SELECT * FROM users WHERE created_at > '1/1/23';
```

### Good

```sql
-- ISO 8601 format - unambiguous and locale-independent
SELECT * FROM orders WHERE order_date = '2023-12-31';

-- Another valid ISO format
SELECT * FROM users WHERE created_at > '2023-01-01';

-- YYYYMMDD format - also unambiguous
SELECT * FROM logs WHERE log_date = '20231231';

-- Using date functions
SELECT * FROM events WHERE event_date = CONVERT(DATE, '2024-03-15');
```

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "avoid-ambiguous-datetime-literal", "enabled": false }
  ]
}
```

## See Also

- [avoid-magic-convert-style-for-datetime](../style/avoid-magic-convert-style-for-datetime.md) - Warns on datetime CONVERT style numbers
- [utc-datetime](../performance/utc-datetime.md) - Detects local datetime functions and suggests UTC alternatives
