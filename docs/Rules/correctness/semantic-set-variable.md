# Semantic Set Variable

**Rule ID:** `semantic/set-variable`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Recommends using `SELECT` for variable assignment instead of `SET`, to keep variable-assignment style consistent across a codebase.

## Rationale

Using a consistent variable-assignment pattern improves readability, especially in codebases that commonly assign variables from queries and/or assign multiple variables at once.

Note: `SET` and `SELECT` do not behave identically in all cases (e.g., multi-row assignments). If your team prefers stricter semantics, consider disabling this rule.

## Examples

### Bad

```sql
DECLARE @Count INT; SET @Count = 10;
```

### Good

```sql
DECLARE @Count INT; SELECT @Count = COUNT(*) FROM Users;
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
    { "id": "semantic/set-variable", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
