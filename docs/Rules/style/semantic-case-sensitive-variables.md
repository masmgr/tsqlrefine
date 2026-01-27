# Semantic Case Sensitive Variables

**Rule ID:** `semantic/case-sensitive-variables`
**Category:** Style
**Severity:** Warning
**Fixable:** No

## Description

Ensures variable references match the exact casing used in their declarations for consistency.

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
DECLARE @UserName NVARCHAR(50);
SET @USERNAME = 'John';  -- Inconsistent casing
```

### Good

```sql
DECLARE @UserName NVARCHAR(50);
SET @UserName = 'John';  -- Consistent casing
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
    { "id": "semantic/case-sensitive-variables", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
