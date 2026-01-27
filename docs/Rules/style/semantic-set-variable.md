# Semantic Set Variable

**Rule ID:** `semantic/set-variable`
**Category:** Style
**Severity:** Information
**Fixable:** No

## Description

Recommends using SELECT for variable assignment instead of SET for better performance and consistency.

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
DECLARE @Count INT; SET @Count = 10;
```

### Good

```sql
DECLARE @Count INT; SELECT @Count = COUNT(*) FROM Users;
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
    { "id": "semantic/set-variable", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
