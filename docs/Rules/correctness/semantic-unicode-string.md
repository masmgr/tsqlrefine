# Semantic Unicode String

**Rule ID:** `semantic/unicode-string`
**Category:** Correctness
**Severity:** Error
**Fixable:** Yes

## Description

Detects Unicode characters in string literals assigned to non-Unicode (VARCHAR/CHAR) variables, which may cause data loss.

## Rationale

This rule prevents code that may produce incorrect results or runtime errors. Following this rule helps ensure your SQL code behaves as expected and produces reliable results.

## Examples

### Bad

```sql
DECLARE @Name VARCHAR(50); SET @Name = 'こんにちは';
```

### Good

```sql
DECLARE @Name NVARCHAR(50); SET @Name = 'こんにちは';
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
    { "id": "semantic/unicode-string", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
