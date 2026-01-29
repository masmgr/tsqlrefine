# Prefer Concat Ws

**Rule ID:** `prefer-concat-ws`
**Category:** Modernization
**Severity:** Information
**Fixable:** No

## Description

Recommends CONCAT_WS() when concatenation repeats the same separator literal; improves readability and reduces duplication (SQL Server 2017+).

## Rationale

CONCAT_WS() (Concatenate With Separator) eliminates repetitive separator literals, improving readability and reducing errors.

**Benefits of CONCAT_WS**:

1. **Eliminates repetition**: Separator specified once, not repeated between every value
2. **NULL handling**: Automatically skips NULL values (no need for ISNULL/COALESCE)
3. **Cleaner code**: More concise and easier to read
4. **Fewer errors**: Can't accidentally use wrong separator or forget one

**Compatibility**: SQL Server 2017+ (compatibility level 140+)

## Examples

### Bad

```sql
-- Repetitive separator with CONCAT (verbose)
SELECT CONCAT(FirstName, ',', LastName, ',', Email) FROM Users;

-- Even worse with + operator (NULL breaks entire concatenation)
SELECT FirstName + ',' + LastName + ',' + Email FROM Users;  -- Returns NULL if any value is NULL

-- Complex with multiple CONCAT calls
SELECT CONCAT(City, ', ', State, ', ', Country) AS Location FROM Addresses;

-- Handling NULLs manually (verbose and error-prone)
SELECT CONCAT(ISNULL(FirstName, ''), ',', ISNULL(LastName, ''), ',', ISNULL(Email, '')) FROM Users;
```

### Good

```sql
-- CONCAT_WS eliminates separator repetition
SELECT CONCAT_WS(',', FirstName, LastName, Email) FROM Users;

-- Automatically handles NULLs (skips NULL values)
SELECT CONCAT_WS(',', FirstName, MiddleName, LastName) FROM Users;
-- Result: "John,Doe" if MiddleName is NULL (no extra comma)

-- Multiple columns with different separators
SELECT
    CONCAT_WS(', ', City, State, Country) AS Location,
    CONCAT_WS(' - ', OrderId, OrderDate, CustomerName) AS OrderInfo
FROM Orders;

-- Complex formatting made simple
SELECT CONCAT_WS(' | ', UserId, UserName, Email, PhoneNumber) AS UserRecord
FROM Users;
```

**Comparison**:

| Approach | NULL Handling | Readability | SQL Server Version |
|----------|---------------|-------------|-------------------|
| `+` operator | Returns NULL if any NULL | Poor | All versions |
| `CONCAT()` | Returns empty string | Good | 2012+ (110+) |
| `CONCAT_WS()` | Skips NULL values | Best | 2017+ (140+) |

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
    { "id": "prefer-concat-ws", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
