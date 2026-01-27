# Prefer Try Convert Patterns

**Rule ID:** `prefer-try-convert-patterns`
**Category:** Modernization
**Severity:** Information
**Fixable:** No

## Description

Recommends TRY_CONVERT/TRY_CAST over CASE + ISNUMERIC/ISDATE; fewer false positives and clearer intent.

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT CASE WHEN ISNUMERIC(@value) = 1 THEN CONVERT(INT, @value) ELSE NULL END;
```

### Good

```sql
SELECT TRY_CONVERT(INT, @value);
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
