# Nested Block Comments

**Rule ID:** `nested-block-comments`
**Category:** Style
**Severity:** Warning
**Fixable:** No

## Description

Avoid nested block comments (/* /* */ */).

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
/* outer /* inner */ outer */\nSELECT 1;
```

### Good

```sql
/* simple comment */\nSELECT 1;
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
    { "id": "nested-block-comments", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
