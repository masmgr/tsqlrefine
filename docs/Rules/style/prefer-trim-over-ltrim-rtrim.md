# Prefer Trim Over Ltrim Rtrim

**Rule ID:** `prefer-trim-over-ltrim-rtrim`
**Category:** Modernization
**Severity:** Information
**Fixable:** No

## Description

Recommends TRIM(x) instead of LTRIM(RTRIM(x)); clearer and less error-prone (SQL Server 2017+).

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT LTRIM(RTRIM(name)) FROM users;
```

### Good

```sql
SELECT TRIM(name) FROM users;
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
    { "id": "prefer-trim-over-ltrim-rtrim", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
