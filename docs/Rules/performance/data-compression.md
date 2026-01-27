# Data Compression

**Rule ID:** `data-compression`
**Category:** Performance
**Severity:** Information
**Fixable:** No

## Description

Recommend specifying DATA_COMPRESSION option in CREATE TABLE for storage optimization

## Rationale

This rule identifies patterns that can cause performance issues. Following this rule helps ensure your queries run efficiently and scale well with data growth.

## Examples

### Bad

```sql
CREATE TABLE dbo.Users (Id INT, Name VARCHAR(100))
```

### Good

```sql
SELECT * FROM users
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
    { "id": "data-compression", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
