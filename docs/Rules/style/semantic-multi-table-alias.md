# Semantic Multi Table Alias

**Rule ID:** `semantic/multi-table-alias`
**Category:** Style
**Severity:** Warning
**Fixable:** No

## Description

Requires column references in multi-table queries (with JOINs) to be qualified with table aliases for clarity.

## Rationale

Qualifying column references in multi-table queries prevents ambiguity and improves maintainability.

**Why qualify in multi-table queries**:

1. **Prevents ambiguity**: Immediately clear which table each column belongs to
2. **Avoids future conflicts**: If another table adds a column with the same name, query doesn't break
3. **Improves readability**: No need to check schema to understand query
4. **Easier maintenance**: Refactoring and debugging are simpler

**When this rule applies**: Only when multiple tables are referenced (JOINs). Single-table queries don't need qualification.

## Examples

### Bad

```sql
-- Multi-table query with unqualified columns
SELECT Id, Name  -- Which Id? Users.Id or Orders.Id?
FROM Users u
JOIN Orders o ON u.Id = o.UserId;

-- Ambiguous column reference
SELECT Id, Total, Status
FROM Users u
JOIN Orders o ON u.Id = o.UserId
WHERE Active = 1;  -- Which Active? Ambiguous!
```

### Good

```sql
-- All columns qualified with table alias (clear)
SELECT u.Id, u.Name, o.OrderId, o.Total
FROM Users u
JOIN Orders o ON u.Id = o.UserId;

-- Complex multi-table query with full qualification
SELECT u.Id AS UserId, u.Name, o.OrderId, o.Total, o.Status
FROM Users u
JOIN Orders o ON u.Id = o.UserId
WHERE u.Active = 1 AND o.Status = 'Pending';

-- Single table query (no qualification needed)
SELECT Id, Name FROM Users;  -- OK, single table
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
    { "id": "semantic/multi-table-alias", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
