# Prefer Coalesce Over Nested Isnull

**Rule ID:** `prefer-coalesce-over-nested-isnull`
**Category:** Modernization
**Severity:** Information
**Fixable:** No

## Description

Detects nested ISNULL and recommends COALESCE; reduces nesting and aligns with standard SQL behavior.

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT ISNULL(ISNULL(@value1, @value2), @value3) FROM users;
```

### Good

```sql
SELECT ISNULL(@value, 'default') FROM users;
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
    { "id": "prefer-coalesce-over-nested-isnull", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
