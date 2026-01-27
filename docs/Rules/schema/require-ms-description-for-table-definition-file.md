# Require Ms Description For Table Definition File

**Rule ID:** `require-ms-description-for-table-definition-file`
**Category:** Schema Design
**Severity:** Information
**Fixable:** No

## Description

Ensures table definition files include an MS_Description extended property so schema intent is captured alongside DDL.

## Rationale

This rule enforces database schema best practices. Following this rule helps create robust, maintainable database schemas.

## Examples

### Bad

```sql
CREATE TABLE dbo.Users (
    Id INT PRIMARY KEY,
    Name NVARCHAR(100)
);
-- Missing MS_Description
```

### Good

```sql
CREATE TABLE dbo.Users (
    Id INT PRIMARY KEY,
    Name NVARCHAR(100)
);

EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'User accounts table',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'Users';
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
    { "id": "require-ms-description-for-table-definition-file", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
