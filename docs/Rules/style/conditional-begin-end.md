# Conditional Begin End

**Rule ID:** `conditional-begin-end`
**Category:** Style
**Severity:** Information
**Fixable:** No

## Description

Require BEGIN/END blocks in conditional statements for clarity and maintainability

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
IF @x = 1 SELECT 1
```

### Good

```sql
-- Example showing compliant code
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
    { "id": "conditional-begin-end", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
