# Count Star

**Rule ID:** `count-star`
**Category:** Style
**Severity:** Information
**Fixable:** No

## Description

Detects COUNT(*) usage and suggests using COUNT(1) or COUNT(column_name) for better clarity and consistency

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT COUNT(*) FROM users;
```

### Good

```sql
SELECT COUNT(user_id) FROM users;
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
    { "id": "count-star", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
