# Prefer Concat Over Plus When Nullable Or Convert

**Rule ID:** `prefer-concat-over-plus-when-nullable-or-convert`
**Category:** Modernization
**Severity:** Information
**Fixable:** No

## Description

Stricter variant that also detects CAST/CONVERT in concatenations; enable instead of prefer-concat-over-plus for comprehensive coverage (SQL Server 2012+).

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT ISNULL(first_name, '') + ' ' + ISNULL(last_name, '') AS full_name FROM users;
```

### Good

```sql
SELECT CONCAT(first_name, ' ', last_name) AS full_name FROM users;
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
    { "id": "prefer-concat-over-plus-when-nullable-or-convert", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
