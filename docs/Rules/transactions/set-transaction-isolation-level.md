# Set Transaction Isolation Level

**Rule ID:** `set-transaction-isolation-level`
**Category:** Configuration
**Severity:** Warning
**Fixable:** No

## Description

Files should start with SET TRANSACTION ISOLATION LEVEL within the first 10 statements.

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
-- Example showing rule violation
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
    { "id": "set-transaction-isolation-level", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
