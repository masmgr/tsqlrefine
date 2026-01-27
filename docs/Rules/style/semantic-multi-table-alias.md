# Semantic Multi Table Alias

**Rule ID:** `semantic/multi-table-alias`
**Category:** Style
**Severity:** Warning
**Fixable:** No

## Description

Requires column references in multi-table queries (with JOINs) to be qualified with table aliases for clarity.

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT Id FROM Users u JOIN Orders o ON u.Id = o.UserId;
```

### Good

```sql
SELECT Id, Name FROM Users;
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
    { "id": "semantic/multi-table-alias", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
