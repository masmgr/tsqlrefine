# Semantic Schema Qualify

**Rule ID:** `semantic/schema-qualify`
**Category:** Style
**Severity:** Warning
**Fixable:** No

## Description

Requires all table references to include schema qualification (e.g., dbo.Users) for clarity and to avoid ambiguity.

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT * FROM Users;
```

### Good

```sql
SELECT * FROM dbo.Users;
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
    { "id": "semantic/schema-qualify", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
