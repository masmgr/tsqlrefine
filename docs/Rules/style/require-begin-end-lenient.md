# Require Begin End Lenient

**Rule ID:** `require-begin-end-lenient`
**Category:** Control Flow Safety
**Severity:** Warning
**Fixable:** No

## Description

Enforces BEGIN/END for IF/ELSE blocks, while allowing a single control-flow statement (e.g., RETURN) without a block.

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
IF @value > 0
    SELECT * FROM Users;
ELSE
    SELECT * FROM Orders;
```

### Good

```sql
IF @value > 0
BEGIN
    SELECT * FROM Users;
END
ELSE
BEGIN
    SELECT * FROM Orders;
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
    { "id": "require-begin-end-lenient", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
