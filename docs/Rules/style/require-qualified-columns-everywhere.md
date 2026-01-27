# Require Qualified Columns Everywhere

**Rule ID:** `require-qualified-columns-everywhere`
**Category:** Query Structure
**Severity:** Warning
**Fixable:** No

## Description

Requires column qualification in WHERE / JOIN / ORDER BY when multiple tables are referenced; stricter than qualified-select-columns.

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT id FROM users WHERE active = 1;
```

### Good

```sql
SELECT name FROM users;
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
    { "id": "require-qualified-columns-everywhere", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
