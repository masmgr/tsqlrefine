# Semantic Left Join Filtered By Where

**Rule ID:** `semantic/left-join-filtered-by-where`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects LEFT JOIN operations where the WHERE clause filters the right-side table, effectively making it an INNER JOIN.

## Rationale

This rule prevents code that may produce incorrect results or runtime errors. Following this rule helps ensure your SQL code behaves as expected and produces reliable results.

## Examples

### Bad

```sql
SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.status = 1
```

### Good

```sql
SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t1.status = 1
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
    { "id": "semantic/left-join-filtered-by-where", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
