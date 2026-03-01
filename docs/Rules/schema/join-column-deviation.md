# Join Column Deviation

**Rule ID:** `join-column-deviation`
**Category:** Schema
**Severity:** Warning
**Fixable:** No

## Description

Detects JOINs where the column combination deviates from the dominant pattern observed in the relation profile.

## Rationale

When a codebase consistently uses specific column combinations to join two tables (e.g., `Orders.UserId = Users.Id` in 90% of cases), a query that joins the same tables on different columns (e.g., `Orders.Amount = Users.Id`) may indicate a copy-paste error or misunderstanding of the data model.

This rule compares the JOIN column combination in each query against a pre-collected relation profile (`relations.json`) that records how tables are actually joined across the codebase. It flags three scenarios:

- **Rare/VeryRare/Structural patterns**: The column combination exists in the profile but is statistically uncommon or structurally different from the dominant pattern.
- **Unseen patterns**: The table pair is known in the profile but this specific column combination was never observed.
- **Unknown table pairs**: The table pair itself does not appear in the relation profile at all.

This rule requires both a schema snapshot (`--schema`) and a relation profile (`--relations-profile`) to function. Without either, it silently produces no diagnostics.

## Examples

### Bad

```sql
-- Rare pattern: Amount=Id is uncommon compared to the dominant UserId=Id
SELECT o.OrderId, u.Name
FROM dbo.Orders AS o
INNER JOIN dbo.Users AS u ON u.Id = o.Amount;

-- Unseen pattern: FULL JOIN on UserId=Id was never observed
SELECT o.OrderId, u.Name
FROM dbo.Orders AS o
FULL JOIN dbo.Users AS u ON u.Id = o.UserId;

-- Unknown table pair: Orders + Products not in the relation profile
SELECT o.OrderId, p.Name
FROM dbo.Orders AS o
INNER JOIN dbo.Products AS p ON p.Id = o.ProductId;
```

### Good

```sql
-- Dominant pattern: the most common JOIN for this table pair
SELECT o.OrderId, u.Name
FROM dbo.Orders AS o
INNER JOIN dbo.Users AS u ON u.Id = o.UserId;

-- Common pattern: above the rare threshold
SELECT o.OrderId, u.Name
FROM dbo.Orders AS o
INNER JOIN dbo.Users AS u ON u.Id = o.CreatedBy;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "join-column-deviation", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
