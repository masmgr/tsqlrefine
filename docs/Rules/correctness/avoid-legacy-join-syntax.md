# Avoid Legacy Join Syntax

**Rule ID:** `avoid-legacy-join-syntax`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Detects legacy outer join syntax (`*=`, `=*`) which is deprecated since SQL Server 2000 and produces incorrect results.

## Rationale

The `*=` (left outer join) and `=*` (right outer join) operators are:
- **Deprecated** since SQL Server 2000 (over 20 years old)
- **Not supported** in SQL Server 2012+ compatibility modes
- **Produce incorrect results** in complex queries with multiple joins
- **Difficult to read** and maintain compared to modern ANSI join syntax

Using these operators indicates severely outdated code that needs immediate migration to modern SQL standards.

## Examples

### Bad

```sql
-- Legacy left outer join
SELECT *
FROM Orders o, Customers c
WHERE o.CustomerId *= c.Id;

-- Legacy right outer join
SELECT *
FROM Orders o, Customers c
WHERE o.CustomerId =* c.Id;
```

### Good

```sql
-- Modern ANSI left outer join
SELECT *
FROM Orders o
LEFT JOIN Customers c ON o.CustomerId = c.Id;

-- Modern ANSI right outer join
SELECT *
FROM Orders o
RIGHT JOIN Customers c ON o.CustomerId = c.Id;
```

## Common Patterns

### Multiple Joins

**Bad:**
```sql
SELECT *
FROM Orders o, Customers c, Products p
WHERE o.CustomerId *= c.Id
  AND o.ProductId *= p.Id;
```

**Good:**
```sql
SELECT *
FROM Orders o
LEFT JOIN Customers c ON o.CustomerId = c.Id
LEFT JOIN Products p ON o.ProductId = p.Id;
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
    { "id": "avoid-legacy-join-syntax", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Microsoft Documentation: FROM clause + JOIN](https://docs.microsoft.com/en-us/sql/t-sql/queries/from-transact-sql)
