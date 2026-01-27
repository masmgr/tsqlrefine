# Keyword Capitalization

**Rule ID:** `keyword-capitalization`
**Category:** Style
**Severity:** Information
**Fixable:** No

## Description

SQL keywords should be in uppercase.

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
select id from users;
```

### Good

```sql
SELECT id FROM users;
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
    { "id": "keyword-capitalization", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
