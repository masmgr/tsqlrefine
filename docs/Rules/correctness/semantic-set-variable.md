# Semantic Set Variable

**Rule ID:** `semantic/set-variable`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Recommends using `SELECT` for variable assignment instead of `SET`, to keep variable-assignment style consistent across a codebase.

## Rationale

SET and SELECT have different behaviors for variable assignment, but both are valid T-SQL syntax. This rule enforces consistency rather than preventing errors.

**Behavior differences**:

| Aspect | SET | SELECT |
|--------|-----|--------|
| Multiple variables | One at a time | Multiple at once |
| Query returns 0 rows | Variable unchanged | Variable set to NULL |
| Query returns 2+ rows | Error | Uses last row (non-deterministic) |
| Clarity | Explicit assignment | Can mix with query logic |

**Multi-row query behavior**:
```sql
-- SET: Error if query returns multiple rows
DECLARE @Name VARCHAR(50);
SET @Name = (SELECT Name FROM Users);  -- Error if >1 row

-- SELECT: Uses last row (non-deterministic)
DECLARE @Name VARCHAR(50);
SELECT @Name = Name FROM Users;  -- No error, uses last row
```

**When to use SET**:
- Simple scalar assignments: `SET @Counter = 0`
- Calculations: `SET @Total = @Price * @Quantity`
- When you want errors on multi-row results

**When to use SELECT**:
- Assigning multiple variables: `SELECT @Id = Id, @Name = Name FROM Users WHERE ...`
- Assigning from queries with TOP 1: `SELECT @Name = Name FROM Users ORDER BY Id DESC`
- Consistency in codebases that prefer SELECT style

**This rule's purpose**: Enforce consistent style across codebase, not prevent errors.

## Examples

### Bad

```sql
-- Uses SET for variable assignment
DECLARE @Count INT;
SET @Count = 10;

-- Another example with calculation
DECLARE @Total DECIMAL(10,2);
SET @Total = 100.00 * 1.15;
```

### Good

```sql
-- Uses SELECT for variable assignment (consistent style)
DECLARE @Count INT;
SELECT @Count = 10;

-- Multiple variable assignment (SELECT advantage)
DECLARE @Id INT, @Name VARCHAR(50);
SELECT @Id = Id, @Name = Name FROM Users WHERE UserId = 1;

-- Assignment from query
DECLARE @Total DECIMAL(10,2);
SELECT @Total = SUM(Amount) FROM Orders WHERE Status = 'Completed';
```

**Note**: Both Bad and Good examples are functionally correct in their basic form. This is a style preference, not a correctness issue.

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
    { "id": "semantic-set-variable", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
