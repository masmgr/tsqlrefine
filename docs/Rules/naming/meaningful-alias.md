# Meaningful Alias

**Rule ID:** `meaningful-alias`
**Category:** Naming
**Severity:** Information
**Fixable:** No

## Description

Use meaningful aliases instead of single-character aliases in multi-table queries

## Rationale

This rule enforces naming conventions that improve code readability and maintainability. Following this rule makes your code easier to understand for other developers.

## Examples

### Bad

```sql
SELECT * FROM users usr JOIN orders o ON usr.id = o.user_id;
```

### Good

```sql
SELECT * FROM users usr JOIN orders ord ON usr.id = ord.user_id;
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
    { "id": "meaningful-alias", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
