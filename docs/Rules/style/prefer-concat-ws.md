# Prefer Concat Ws

**Rule ID:** `prefer-concat-ws`
**Category:** Modernization
**Severity:** Information
**Fixable:** No

## Description

Recommends CONCAT_WS() when concatenation repeats the same separator literal; improves readability and reduces duplication (SQL Server 2017+).

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
-- Example showing rule violation
```

### Good

```sql
SELECT CONCAT_WS(',', first_name, last_name, email) FROM users;
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
    { "id": "prefer-concat-ws", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
