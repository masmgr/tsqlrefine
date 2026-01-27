# Named Constraint

**Rule ID:** `named-constraint`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Prohibit named constraints in temp tables to avoid naming conflicts

## Rationale

This rule prevents code that may produce incorrect results or runtime errors. Following this rule helps ensure your SQL code behaves as expected and produces reliable results.

## Examples

### Bad

```sql
CREATE TABLE #TempUsers (
    Id INT CONSTRAINT PK_TempUsers PRIMARY KEY  -- Named constraint in temp table
);
```

### Good

```sql
CREATE TABLE #TempUsers (
    Id INT PRIMARY KEY  -- Unnamed constraint in temp table
);
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
    { "id": "named-constraint", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
