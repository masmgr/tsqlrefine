# Prefer String Agg Over Stuff

**Rule ID:** `prefer-string-agg-over-stuff`
**Category:** Modernization
**Severity:** Information
**Fixable:** No

## Description

Recommends STRING_AGG() over STUFF(... FOR XML PATH('') ...); simpler and typically faster/safer (SQL Server 2017+).

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT STUFF((SELECT '|' + col FROM data FOR XML PATH('')), 1, 1, '') AS result;
```

### Good

```sql
SELECT STRING_AGG(name, ',') AS names FROM users;
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
    { "id": "prefer-string-agg-over-stuff", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
