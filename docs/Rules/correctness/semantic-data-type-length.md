# Semantic Data Type Length

**Rule ID:** `semantic/data-type-length`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Requires explicit length specification for variable-length data types (VARCHAR, NVARCHAR, CHAR, NCHAR, VARBINARY, BINARY).

## Rationale

This rule prevents code that may produce incorrect results or runtime errors. Following this rule helps ensure your SQL code behaves as expected and produces reliable results.

## Examples

### Bad

```sql
DECLARE @Name VARCHAR;
```

### Good

```sql
DECLARE @Name VARCHAR(50);
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
    { "id": "semantic/data-type-length", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
