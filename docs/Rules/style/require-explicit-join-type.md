# Require Explicit Join Type

**Rule ID:** `require-explicit-join-type`
**Category:** Query Structure
**Severity:** Warning
**Fixable:** Yes

## Description

Disallows ambiguous JOIN shorthand; makes JOIN semantics explicit and consistent across a codebase.

## Rationale

This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.

## Examples

### Bad

```sql
SELECT * FROM dbo.TableA JOIN dbo.TableB ON TableA.Id = TableB.Id;
```

### Good

```sql
SELECT * FROM dbo.TableA INNER JOIN dbo.TableB ON TableA.Id = TableB.Id;
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
    { "id": "require-explicit-join-type", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
