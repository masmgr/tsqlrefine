# Len For Emptiness Check

**Rule ID:** `len-for-emptiness-check`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Warns when LEN() is used in an emptiness comparison; trailing spaces are ignored, so use DATALENGTH() to detect whitespace-only values.

## Rationale

`LEN()` returns the number of characters **excluding trailing spaces**. As a result, a value consisting only of spaces — including full-width (double-byte) spaces — is reported as length `0`, even though the string is not actually empty. Code that checks emptiness with `LEN(x) = 0` (or non-emptiness with `LEN(x) > 0`) will therefore misclassify whitespace-only values, which can lead to subtle bugs such as a "blank" item name that never satisfies a guard condition and causes an infinite loop.

Use `DATALENGTH()` to count the actual bytes when you need to reliably detect empty or whitespace-only values.

This rule fires when `LEN(...)` is compared against zero with any comparison operator (`=`, `<>`, `>`, `>=`, `<`, `<=`). Comparisons against other numeric literals (for example `LEN(code) < 5`) are not flagged, because those are character-count checks where replacing `LEN()` with `DATALENGTH()` would change the meaning for Unicode data.

## Examples

### Bad

```sql
-- Whitespace-only Name slips through as "empty".
SELECT * FROM dbo.Products WHERE LEN(Name) = 0;

-- Same problem on the non-empty check.
SELECT * FROM dbo.Products WHERE LEN(Name) > 0;
```

### Good

```sql
-- DATALENGTH reliably detects whitespace-only values.
SELECT * FROM dbo.Products WHERE DATALENGTH(Name) = 0;

-- Direct string comparison.
SELECT * FROM dbo.Products WHERE Name = '';
```

## Configuration

To disable this rule:

```json
{
  "rules": {
    "len-for-emptiness-check": "none"
  }
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
