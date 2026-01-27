# Require Begin End For While

**Rule ID:** `require-begin-end-for-while`
**Category:** Control Flow Safety
**Severity:** Warning
**Fixable:** No

## Description

Enforces BEGIN/END for every WHILE body to avoid accidental single-statement loops when code is edited.

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
DECLARE @counter INT = 0;
WHILE @counter < 10
    SET @counter = @counter + 1;
```

### Good

```sql
DECLARE @counter INT = 0;
WHILE @counter < 10
BEGIN
    SET @counter = @counter + 1;
END
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
    { "id": "require-begin-end-for-while", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
