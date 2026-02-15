# Avoid Order By Ordinal

**Rule ID:** `avoid-order-by-ordinal`
**Category:** Style
**Severity:** Information
**Fixable:** No

## Description

Forbids ORDER BY with ordinal positions (e.g., ORDER BY 1, 2) which break silently when columns are reordered.

## Rationale

Using ordinal positions in `ORDER BY` clauses creates a fragile dependency on the column order of the `SELECT` list:

1. **Silent breakage**: Adding, removing, or reordering columns in the `SELECT` list changes the meaning of ordinal references without any error or warning
2. **Poor readability**: `ORDER BY 1, 2` doesn't communicate intent; reviewers must mentally count columns to understand the sort order
3. **Maintenance burden**: Any change to the `SELECT` list requires re-verifying all ordinal references in `ORDER BY`
4. **Static detection**: This anti-pattern is easily caught by static analysis, saving review time

## Examples

### Bad

```sql
-- Ordinal positions break when columns are reordered
SELECT id, name, email FROM dbo.Users ORDER BY 1, 2;

-- Mixed ordinal and name
SELECT id, name FROM dbo.Users ORDER BY 1, name;

-- Ordinal with sort direction
SELECT id, name FROM dbo.Users ORDER BY 1 DESC;
```

### Good

```sql
-- Explicit column names are clear and resilient
SELECT id, name, email FROM dbo.Users ORDER BY id, name;

-- Expressions are fine
SELECT id, name FROM dbo.Users ORDER BY LEN(name);

-- Alias references are acceptable
SELECT id, name AS display_name FROM dbo.Users ORDER BY display_name;
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
    { "id": "avoid-order-by-ordinal", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
