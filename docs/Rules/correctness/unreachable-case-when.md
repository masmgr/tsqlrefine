# Unreachable CASE WHEN

**Rule ID:** `unreachable-case-when`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects duplicate WHEN conditions in CASE expressions that make later branches unreachable.

## Rationale

In a CASE expression, SQL Server evaluates WHEN branches in order and returns the result of the first matching condition. If the same condition appears more than once, any subsequent branches with that condition are unreachable — they will never execute. This typically indicates a copy-paste error or logic bug. By flagging duplicate WHEN conditions, this rule helps catch mistakes early before they lead to incorrect query results.

The rule handles both simple CASE (`CASE @x WHEN 1 ...`) and searched CASE (`CASE WHEN @x = 1 ...`) expressions. Condition matching is case-insensitive and whitespace-normalized.

## Examples

### Bad

```sql
-- Duplicate WHEN value in simple CASE
SELECT CASE @x
    WHEN 1 THEN 'a'
    WHEN 1 THEN 'b'  -- unreachable, first WHEN 1 always wins
END;

-- Duplicate condition in searched CASE
SELECT CASE
    WHEN @x = 1 THEN 'a'
    WHEN @x = 1 THEN 'b'  -- unreachable
END;

-- Multiple duplicate conditions
SELECT CASE @x
    WHEN 1 THEN 'a'
    WHEN 2 THEN 'b'
    WHEN 1 THEN 'c'  -- unreachable
    WHEN 2 THEN 'd'  -- unreachable
END;
```

### Good

```sql
-- Distinct WHEN values
SELECT CASE @x
    WHEN 1 THEN 'a'
    WHEN 2 THEN 'b'
    WHEN 3 THEN 'c'
END;

-- Distinct searched conditions
SELECT CASE
    WHEN @x = 1 THEN 'a'
    WHEN @x = 2 THEN 'b'
END;

-- CASE with ELSE
SELECT CASE @x
    WHEN 1 THEN 'a'
    ELSE 'b'
END;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "unreachable-case-when", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
