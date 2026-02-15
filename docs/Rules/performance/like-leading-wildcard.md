# LIKE Leading Wildcard

**Rule ID:** `like-leading-wildcard`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Detects LIKE patterns with a leading wildcard (%, _, [) in predicates, which prevents index usage and causes full table scans.

## Rationale

When a `LIKE` pattern starts with a wildcard character (`%`, `_`, or `[`), SQL Server cannot use an index seek on the column. Instead, it must perform a full table or index scan, which can be extremely expensive on large tables.

This is one of the most common non-sargable patterns and a frequent cause of performance issues in production.

- `LIKE '%value'` - Leading `%` forces a full scan
- `LIKE '_value'` - Leading `_` forces a full scan
- `LIKE '[A-Z]value'` - Leading bracket wildcard forces a full scan
- `LIKE 'value%'` - Trailing `%` CAN use an index seek (this is fine)

## Examples

### Bad

```sql
-- Leading % prevents index usage
SELECT * FROM dbo.Users WHERE Name LIKE '%smith';
SELECT * FROM dbo.Users WHERE Email LIKE '%@gmail.com';
SELECT * FROM dbo.Users WHERE Name LIKE '%smith%';

-- Leading _ prevents index usage
SELECT * FROM dbo.Users WHERE Code LIKE '_BC';

-- Leading bracket wildcard prevents index usage
SELECT * FROM dbo.Users WHERE Code LIKE '[A-Z]BC';
```

### Good

```sql
-- Trailing wildcard allows index seek
SELECT * FROM dbo.Users WHERE Name LIKE 'smith%';

-- No wildcard - exact match
SELECT * FROM dbo.Users WHERE Name LIKE 'smith';

-- Wildcard not at start
SELECT * FROM dbo.Users WHERE Name LIKE 'sm%th';

-- For leading wildcard searches, consider full-text search
SELECT * FROM dbo.Users WHERE CONTAINS(Name, 'smith');
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "like-leading-wildcard", "enabled": false }
  ]
}
```

## See Also

- [avoid-non-sargable-predicate](avoid-non-sargable-predicate.md) - Detects functions applied to columns in predicates
- [avoid-upper-lower-in-predicate](avoid-upper-lower-in-predicate.md) - Detects UPPER/LOWER in predicates
- [TsqlRefine Rules Documentation](../README.md)
