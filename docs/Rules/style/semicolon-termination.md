# Semicolon Termination

**Rule ID:** `semicolon-termination`
**Category:** Style
**Severity:** Information
**Fixable:** Yes

## Description

SQL statements should be terminated with a semicolon

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT 1
```

### Good

```sql
SELECT 1;
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
    { "id": "semicolon-termination", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
