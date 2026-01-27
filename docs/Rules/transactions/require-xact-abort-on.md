# Require Xact Abort On

**Rule ID:** `require-xact-abort-on`
**Category:** Transaction Safety
**Severity:** Warning
**Fixable:** No

## Description

Requires SET XACT_ABORT ON with explicit transactions to ensure runtime errors reliably abort and roll back work.

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
BEGIN TRANSACTION; UPDATE users SET active = 1; COMMIT;
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
    { "id": "require-xact-abort-on", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
