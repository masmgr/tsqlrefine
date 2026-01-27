# Semantic Cte Name Conflict

**Rule ID:** `semantic/cte-name-conflict`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Detects CTE name conflicts with other CTEs or table aliases in the same scope.

## Rationale

This rule prevents code that may produce incorrect results or runtime errors. Following this rule helps ensure your SQL code behaves as expected and produces reliable results.

## Examples

### Bad

```sql
WITH cte AS (SELECT 1), cte AS (SELECT 2) SELECT * FROM cte
```

### Good

```sql
WITH cte1 AS (SELECT 1), cte2 AS (SELECT 2) SELECT * FROM cte1 JOIN cte2
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
    { "id": "semantic/cte-name-conflict", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
