# Join Foreign Key Mismatch

**Rule ID:** `join-foreign-key-mismatch`
**Category:** Schema
**Severity:** Warning
**Fixable:** No

## Description

Detects JOINs where the ON columns match a foreign key relationship but the joined table differs from the FK target.

## Rationale

When a table has a foreign key constraint (e.g., `Orders.UserId → Users.Id`), JOIN conditions using those columns should connect the correct tables. If the ON clause uses FK columns but joins to a different table than the FK target, it is almost certainly a bug — the developer likely intended to join to the FK target table but accidentally specified the wrong table.

This rule cross-references the schema snapshot's foreign key definitions with the actual JOIN targets in the query. It checks both directions: outgoing FKs (where the table owns the FK) and incoming/referencing FKs (where the table is referenced by another table's FK).

## Examples

### Bad

```sql
-- Orders.UserId has FK to Users.Id, but query joins to Products instead
SELECT o.OrderId, p.Name
FROM dbo.Orders AS o
INNER JOIN dbo.Products AS p ON p.Id = o.UserId;

-- Same issue with reversed ON clause order
SELECT o.OrderId, p.Name
FROM dbo.Orders AS o
INNER JOIN dbo.Products AS p ON o.UserId = p.Id;
```

### Good

```sql
-- Correct FK target table
SELECT o.OrderId, u.Name
FROM dbo.Orders AS o
INNER JOIN dbo.Users AS u ON u.Id = o.UserId;

-- Columns not part of any FK relationship (no warning)
SELECT a.Name, c.Value
FROM dbo.TableA AS a
INNER JOIN dbo.TableC AS c ON c.Value = a.Name;

-- Self-referencing FK (correct — FK points to same table)
SELECT s1.Id, s2.Id
FROM dbo.SelfRef AS s1
INNER JOIN dbo.SelfRef AS s2 ON s2.Id = s1.ParentId;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "join-foreign-key-mismatch", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
