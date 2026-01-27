# Semantic Join Condition Always True

**Rule ID:** `semantic/join-condition-always-true`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects JOIN conditions that are always true or likely incorrect, such as 'ON 1=1' or self-comparisons.

## Rationale

This rule prevents code that may produce incorrect results or runtime errors. Following this rule helps ensure your SQL code behaves as expected and produces reliable results.

## Examples

### Bad

```sql
SELECT * FROM t1 JOIN t2 ON 1=1
```

### Good

```sql
SELECT * FROM t1 JOIN t2 ON t1.id = t2.id
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
    { "id": "semantic/join-condition-always-true", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
