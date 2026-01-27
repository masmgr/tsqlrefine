# Qualified Select Columns

**Rule ID:** `qualified-select-columns`
**Category:** Query Structure
**Severity:** Warning
**Fixable:** No

## Description

Requires qualifying columns in SELECT lists when multiple tables are referenced; prevents subtle 'wrong table' mistakes when column names overlap.

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT id FROM users u INNER JOIN orders o ON u.id = o.user_id;
```

### Good

```sql
SELECT u.id FROM users u INNER JOIN orders o ON u.id = o.user_id;
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
    { "id": "qualified-select-columns", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
