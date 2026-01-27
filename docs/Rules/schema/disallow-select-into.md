# Disallow Select Into

**Rule ID:** `disallow-select-into`
**Category:** Schema Design
**Severity:** Warning
**Fixable:** No

## Description

Warns on SELECT ... INTO; it implicitly creates schema and can produce fragile, environment-dependent results.

## Rationale

This rule enforces database schema best practices. Following this rule helps create robust, maintainable database schemas.

## Examples

### Bad

```sql
SELECT * INTO #temp FROM users;
```

### Good

```sql
SELECT * FROM users;
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
    { "id": "disallow-select-into", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
