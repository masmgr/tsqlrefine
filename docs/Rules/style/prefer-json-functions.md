# Prefer Json Functions

**Rule ID:** `prefer-json-functions`
**Category:** Modernization
**Severity:** Information
**Fixable:** No

## Description

Encourages built-in JSON features (OPENJSON, JSON_VALUE, FOR JSON, etc.) over manual string parsing/building (SQL Server 2016+).

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT CHARINDEX('{', json_data) FROM data;
```

### Good

```sql
SELECT JSON_VALUE(json_col, '$.name') FROM users;
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
    { "id": "prefer-json-functions", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
