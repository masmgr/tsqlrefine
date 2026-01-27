# Prefer Concat Over Plus

**Rule ID:** `prefer-concat-over-plus`
**Category:** Modernization
**Severity:** Information
**Fixable:** No

## Description

Recommends CONCAT() when + concatenation uses ISNULL/COALESCE; avoids subtle NULL propagation (SQL Server 2012+).

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT ISNULL(@firstName, '') + ' ' + @lastName FROM users;
```

### Good

```sql
SELECT @firstName + ' ' + @lastName FROM users;
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
    { "id": "prefer-concat-over-plus", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
