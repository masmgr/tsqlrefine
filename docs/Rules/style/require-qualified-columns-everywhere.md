# Require Qualified Columns Everywhere

**Rule ID:** `require-qualified-columns-everywhere`
**Category:** Query Structure
**Severity:** Warning
**Fixable:** No

## Description

Requires column qualification in WHERE / JOIN / ORDER BY when multiple tables are referenced; stricter than qualified-select-columns.

## Rationale

Qualifying columns in multi-table queries prevents ambiguity and future schema conflicts.

**Why qualify everywhere in multi-table queries**:

1. **Prevents ambiguity**: Clear which table each column belongs to
2. **Future-proof**: Schema changes (adding columns to other tables) won't break queries
3. **Readability**: Query logic is immediately clear without checking schema
4. **Maintenance**: Easier to refactor and modify queries

**Stricter than `qualified-select-columns`**: This rule requires qualification in WHERE/JOIN/ORDER BY clauses, not just SELECT.

## Examples

### Bad

```sql
-- Multi-table query without full qualification
SELECT u.Name, OrderId  -- OrderId not qualified
FROM Users u
JOIN Orders o ON u.Id = o.UserId
WHERE Active = 1  -- Active not qualified (which table?)
ORDER BY CreatedDate;  -- CreatedDate not qualified

-- Ambiguous in WHERE clause
SELECT u.Name, o.Total
FROM Users u
JOIN Orders o ON u.Id = o.UserId
WHERE Status = 'Active';  -- Status could be in Users or Orders
```

### Good

```sql
-- All columns fully qualified (clear and unambiguous)
SELECT u.Name, o.OrderId
FROM Users u
JOIN Orders o ON u.Id = o.UserId
WHERE u.Active = 1  -- Clearly Users.Active
ORDER BY o.CreatedDate;  -- Clearly Orders.CreatedDate

-- Complex multi-table query with full qualification
SELECT u.Name, o.OrderId, o.Status
FROM Users u
JOIN Orders o ON u.Id = o.UserId
WHERE u.Active = 1 AND o.Status = 'Pending'  -- All qualified
ORDER BY o.OrderDate DESC, u.Name ASC;  -- All qualified

-- Single table query (no qualification needed)
SELECT Name, Email FROM Users WHERE Active = 1;  -- OK, single table
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
    { "id": "require-qualified-columns-everywhere", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
