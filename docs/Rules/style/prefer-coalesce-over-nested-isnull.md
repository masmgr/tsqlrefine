# Prefer Coalesce Over Nested Isnull

**Rule ID:** `prefer-coalesce-over-nested-isnull`
**Category:** Modernization
**Severity:** Information
**Fixable:** No

## Description

Detects nested ISNULL and recommends COALESCE to reduce nesting and improve readability.

## Rationale

COALESCE is more readable and maintainable than nested ISNULL for handling multiple fallback values.

**Why COALESCE is better for multiple values**:

1. **Eliminates nesting**: Single function call instead of nested ISNULLs
2. **Standard SQL**: COALESCE is ANSI SQL standard (portable)
3. **Unlimited arguments**: Can handle any number of fallback values
4. **More readable**: Intent is immediately clear

**ISNULL vs COALESCE**:

| Feature | ISNULL | COALESCE |
|---------|--------|----------|
| Arguments | 2 (fixed) | Unlimited |
| Standard SQL | No (T-SQL only) | Yes (ANSI SQL) |
| Nesting required | Yes (for 3+ values) | No |
| Data type handling | Returns first argument's type | Returns highest precedence type |

**When to use ISNULL**: Simple 2-value scenarios where performance is critical (ISNULL is slightly faster).

**When to use COALESCE**: 3+ values, or when portability matters.

## Examples

### Bad

```sql
-- Nested ISNULL (hard to read)
SELECT ISNULL(ISNULL(@value1, @value2), @value3) FROM Users;

-- Deeply nested ISNULL (very hard to read)
SELECT ISNULL(ISNULL(ISNULL(@primary, @secondary), @tertiary), 'default') AS FinalValue;

-- Complex nesting with table columns
SELECT
    UserId,
    ISNULL(ISNULL(ISNULL(MobilePhone, HomePhone), WorkPhone), 'No phone') AS ContactPhone
FROM Users;
```

### Good

```sql
-- COALESCE (clear and concise)
SELECT COALESCE(@value1, @value2, @value3) FROM Users;

-- Multiple fallbacks with COALESCE (readable)
SELECT COALESCE(@primary, @secondary, @tertiary, 'default') AS FinalValue;

-- Complex fallback logic made simple
SELECT
    UserId,
    COALESCE(MobilePhone, HomePhone, WorkPhone, 'No phone') AS ContactPhone
FROM Users;

-- Single fallback: ISNULL is acceptable
SELECT ISNULL(@value, 'default') FROM Users;  -- OK, only 2 values
```

**Comparison**:

```sql
-- Bad: Nested ISNULL (4 levels deep)
ISNULL(ISNULL(ISNULL(ISNULL(@a, @b), @c), @d), 'default')

-- Good: COALESCE (flat, readable)
COALESCE(@a, @b, @c, @d, 'default')
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
    { "id": "prefer-coalesce-over-nested-isnull", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
