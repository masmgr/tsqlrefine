# Alias Scope Violation

**Rule ID:** `semantic/alias-scope-violation`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects potential scope violations where aliases from outer queries are referenced in inner queries without clear correlation intent.

## Rationale

In SQL, derived tables (subqueries in the FROM clause) have specific scoping rules. A derived table cannot reference table aliases that are defined later in the same FROM clause of the outer query. This can lead to:

- **Runtime errors**: The query may fail to execute if the alias is not yet in scope
- **Unexpected behavior**: The query may reference a different table than intended if an alias with the same name exists in an outer scope
- **Maintenance issues**: The code becomes confusing and hard to understand when scope rules are violated

This rule helps catch these scope violations early, preventing runtime errors and ensuring queries behave as expected.

## Examples

### Bad

```sql
-- Derived table references t2 which is defined AFTER the derived table
SELECT * FROM (SELECT * FROM t1 WHERE t2.id = 1) x JOIN t2 ON 1=1;

-- Subquery references t3 from outer scope before it's available
SELECT * FROM (SELECT t3.col FROM t1) x, t2, t3;
```

### Good

```sql
-- Valid correlated subquery - outer table referenced properly
SELECT * FROM orders o
WHERE EXISTS (SELECT 1 FROM order_items oi WHERE oi.order_id = o.id);

-- Valid derived table with proper references
SELECT * FROM (SELECT * FROM t1) x JOIN t2 ON x.id = t2.id;

-- Valid reference order - tables defined before use
SELECT * FROM t1, (SELECT * FROM t2) x WHERE t1.id = x.id;

-- Valid self-reference within subquery
SELECT * FROM (SELECT * FROM t1 WHERE t1.id = 1) x;
```

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "semantic/alias-scope-violation", "enabled": false }
  ]
}
```

## See Also

- [semantic/undefined-alias](semantic-undefined-alias.md) - Detects references to undefined table aliases
- [semantic/duplicate-alias](semantic-duplicate-alias.md) - Detects duplicate table aliases in the same scope
