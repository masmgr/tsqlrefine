# Print Statement

**Rule ID:** `print-statement`
**Category:** Style
**Severity:** Information
**Fixable:** No

## Description

Prohibit PRINT statements; use RAISERROR for error messages and debugging

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
PRINT 'This is a debug message';
```

### Good

```sql
SELECT 'Hello World'
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
    { "id": "print-statement", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
