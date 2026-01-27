# Semantic Insert Column Count Mismatch

**Rule ID:** `semantic/insert-column-count-mismatch`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Detects column count mismatches between the target column list and the source in INSERT statements.

## Rationale

This rule prevents code that may produce incorrect results or runtime errors. Following this rule helps ensure your SQL code behaves as expected and produces reliable results.

## Examples

### Bad

```sql
INSERT INTO t (a, b) SELECT x, y, z FROM t2
```

### Good

```sql
INSERT INTO t (a, b) SELECT x, y FROM t2
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
    { "id": "semantic/insert-column-count-mismatch", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
