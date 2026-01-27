# Escape Keyword Identifier

**Rule ID:** `escape-keyword-identifier`
**Category:** Correctness
**Severity:** Warning
**Fixable:** Yes

## Description

Warns when a Transact-SQL keyword is used as a table/column identifier without escaping, and offers an autofix to bracket it.

## Rationale

This rule prevents code that may produce incorrect results or runtime errors. Following this rule helps ensure your SQL code behaves as expected and produces reliable results.

## Examples

### Bad

```sql
SELECT * FROM order;
```

### Good

```sql
SELECT * FROM [order];
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
    { "id": "escape-keyword-identifier", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
