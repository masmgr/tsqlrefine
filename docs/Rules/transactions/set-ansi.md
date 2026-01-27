# Set Ansi

**Rule ID:** `set-ansi`
**Category:** Configuration
**Severity:** Warning
**Fixable:** No

## Description

Files should start with SET ANSI_NULLS ON within the first 10 statements.

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
CREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;
```

### Good

```sql
SET ANSI_NULLS ON;\nGO\nSELECT 1;
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
    { "id": "set-ansi", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
