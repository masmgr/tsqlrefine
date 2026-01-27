# Require Column List For Insert Select

**Rule ID:** `require-column-list-for-insert-select`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

INSERT SELECT statements must explicitly specify the column list to avoid errors when table schema changes

## Rationale

This rule prevents code that may produce incorrect results or runtime errors. Following this rule helps ensure your SQL code behaves as expected and produces reliable results.

## Examples

### Bad

```sql
INSERT INTO users SELECT * FROM temp;
```

### Good

```sql
INSERT INTO users (id, name) SELECT id, name FROM temp;
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
    { "id": "require-column-list-for-insert-select", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
