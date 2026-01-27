# Linked Server

**Rule ID:** `linked-server`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Prohibit linked server queries (4-part identifiers); use alternative data access patterns

## Rationale

This rule identifies patterns that can cause performance issues. Following this rule helps ensure your queries run efficiently and scale well with data growth.

## Examples

### Bad

```sql
SELECT * FROM [RemoteServer].[RemoteDB].[dbo].[Users]
```

### Good

```sql
SELECT * FROM dbo.Users
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
    { "id": "linked-server", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
