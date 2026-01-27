# Disallow Cursors

**Rule ID:** `disallow-cursors`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Prohibit cursor usage; prefer set-based operations for better performance

## Rationale

This rule identifies patterns that can cause performance issues. Following this rule helps ensure your queries run efficiently and scale well with data growth.

## Examples

### Bad

```sql
DECLARE cursor_name CURSOR FOR SELECT * FROM users;
```

### Good

```sql
SELECT * FROM users
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
    { "id": "disallow-cursors", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
