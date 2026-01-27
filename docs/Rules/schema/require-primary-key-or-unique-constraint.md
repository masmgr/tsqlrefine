# Require Primary Key Or Unique Constraint

**Rule ID:** `require-primary-key-or-unique-constraint`
**Category:** Schema Design
**Severity:** Warning
**Fixable:** No

## Description

Requires PRIMARY KEY or UNIQUE constraints for user tables; helps enforce correctness and supports indexing/relational integrity.

## Rationale

This rule enforces database schema best practices. Following this rule helps create robust, maintainable database schemas.

## Examples

### Bad

```sql
CREATE TABLE dbo.Users (id INT, name VARCHAR(100));
```

### Good

```sql
CREATE TABLE dbo.Users (id INT PRIMARY KEY, name VARCHAR(100));
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
    { "id": "require-primary-key-or-unique-constraint", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
