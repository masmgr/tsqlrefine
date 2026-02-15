# Avoid Select Into

**Rule ID:** `avoid-select-into`
**Category:** Performance
**Severity:** Information
**Fixable:** No

## Description

Warns on SELECT ... INTO; it implicitly creates schema and can produce fragile, environment-dependent results.

## Rationale

SELECT...INTO implicitly creates schema based on the query result, which can make deployments fragile and the resulting schema harder to reason about. Prefer explicitly defining the table schema and then inserting data to improve predictability and maintainability.

## Examples

### Bad

```sql
SELECT * INTO #temp FROM users;
```

### Good

```sql
CREATE TABLE #temp
(
    user_id INT NOT NULL,
    user_name NVARCHAR(100) NOT NULL
);

INSERT INTO #temp (user_id, user_name)
SELECT user_id, user_name
FROM users;
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
    { "id": "avoid-select-into", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
