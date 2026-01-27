# Require Column List For Insert Values

**Rule ID:** `require-column-list-for-insert-values`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

INSERT VALUES statements must explicitly specify the column list to avoid errors when table schema changes

## Rationale

This rule prevents code that may produce incorrect results or runtime errors. Following this rule helps ensure your SQL code behaves as expected and produces reliable results.

## Examples

### Bad

```sql
INSERT INTO users VALUES (1, 'John');
```

### Good

```sql
INSERT INTO users (id, name) VALUES (1, 'John');
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
    { "id": "require-column-list-for-insert-values", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
