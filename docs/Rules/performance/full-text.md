# Full Text

**Rule ID:** `full-text`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Prohibit full-text search predicates; use alternative search strategies for better performance

## Rationale

This rule identifies patterns that can cause performance issues. Following this rule helps ensure your queries run efficiently and scale well with data growth.

## Examples

### Bad

```sql
SELECT * FROM documents WHERE CONTAINS(content, 'search term')
```

### Good

```sql
SELECT * FROM documents WHERE title LIKE '%search%'
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
    { "id": "full-text", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
