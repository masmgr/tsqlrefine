# Avoid Max Plus One Key Generation

**Rule ID:** `avoid-max-plus-one-key-generation`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects `MAX(...) + positive integer` expressions used in assignments, UPDATE values, or INSERT values. This allocation pattern can issue the same value to concurrent sessions.

## Rationale

Reading the current maximum and calculating the next key is not atomic. Two sessions can observe the same maximum before either writes its result. Wrappers such as `ISNULL`, `COALESCE`, `CAST`, and parentheses do not make the allocation safe.

Prefer an `IDENTITY` column, a `SEQUENCE`, or a deliberately serialized allocator protected by an appropriate transaction and lock.

## Examples

### Bad

```sql
SELECT @next_id = ISNULL(MAX(ItemId), 0) + 1
FROM dbo.Items;

INSERT dbo.Allocation (NextId)
SELECT MAX(ItemId) + 1 FROM dbo.Items;
```

### Good

```sql
SELECT @next_id = NEXT VALUE FOR dbo.ItemIdSequence;

-- Aggregate reporting is not an allocation and is not flagged.
SELECT MAX(ItemId) + 1 AS SuggestedValue FROM dbo.Items;
```

## Configuration

```json
{
  "rules": {
    "avoid-max-plus-one-key-generation": "none"
  }
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
