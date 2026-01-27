# Join Keyword

**Rule ID:** `join-keyword`
**Category:** Style
**Severity:** Warning
**Fixable:** No

## Description

Detects comma-separated table lists in FROM clause (implicit joins) and suggests using explicit JOIN syntax for better readability

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT * FROM users, profiles;
```

### Good

```sql
SELECT * FROM users INNER JOIN profiles ON users.id = profiles.user_id;
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
    { "id": "join-keyword", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
