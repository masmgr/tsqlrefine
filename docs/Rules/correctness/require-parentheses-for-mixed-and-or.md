# Require Parentheses For Mixed And Or

**Rule ID:** `require-parentheses-for-mixed-and-or`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects mixed AND/OR operators at same precedence level without explicit parentheses to prevent precedence confusion.

## Rationale

This rule prevents code that may produce incorrect results or runtime errors. Following this rule helps ensure your SQL code behaves as expected and produces reliable results.

## Examples

### Bad

```sql
SELECT * FROM users WHERE active = 1 AND status = 'ok' OR role = 'admin';
```

### Good

```sql
SELECT * FROM users WHERE (active = 1 AND status = 'ok') OR role = 'admin';
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
    { "id": "require-parentheses-for-mixed-and-or", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
