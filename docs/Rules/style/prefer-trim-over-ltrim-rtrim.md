# Prefer Trim Over Ltrim Rtrim

**Rule ID:** `prefer-trim-over-ltrim-rtrim`
**Category:** Modernization
**Severity:** Information
**Fixable:** No

## Description

Recommends TRIM(x) instead of LTRIM(RTRIM(x)); clearer and less error-prone (SQL Server 2017+).

## Rationale

The nested `LTRIM(RTRIM(x))` pattern is a **legacy workaround** from older SQL Server versions that should be replaced with `TRIM()`:

1. **LTRIM(RTRIM(x)) is verbose and error-prone**:
   - Easy to forget one of the two functions (resulting in partial trimming)
   - Easy to get the nesting order wrong (`RTRIM(LTRIM(x))` works but is inconsistent)
   - Two function calls instead of one

2. **TRIM() is standard SQL**:
   - ANSI SQL standard (portable across databases)
   - Available in SQL Server 2017+ (compat level 140+)
   - Single function call, clearer intent

3. **Additional TRIM() features**:
   - Can trim specific characters: `TRIM('.' FROM '.data.')` → `'data'`
   - Can trim only leading or trailing: `TRIM(LEADING FROM x)`, `TRIM(TRAILING FROM x)`
   - Cannot achieve these with LTRIM/RTRIM

**When LTRIM/RTRIM is still needed:**
- SQL Server 2016 or earlier (TRIM not available)
- Trimming only left or right side (LTRIM only, RTRIM only)

**Compatibility**: `TRIM()` available in SQL Server 2017+ (compat level 140+).

## Examples

### Bad

```sql
-- Nested LTRIM/RTRIM (verbose)
SELECT LTRIM(RTRIM(name)) FROM users;

-- Easy to forget one side
SELECT LTRIM(name) FROM users;  -- Only trims left!

-- Wrong nesting order (still works but inconsistent)
SELECT RTRIM(LTRIM(name)) FROM users;

-- Multiple columns (repetitive)
SELECT
    LTRIM(RTRIM(first_name)) AS first_name,
    LTRIM(RTRIM(last_name)) AS last_name,
    LTRIM(RTRIM(email)) AS email
FROM users;

-- In WHERE clause (hard to read)
WHERE LTRIM(RTRIM(status)) = 'active';
```

### Good

```sql
-- TRIM (clean and simple)
SELECT TRIM(name) FROM users;

-- Multiple columns (consistent)
SELECT
    TRIM(first_name) AS first_name,
    TRIM(last_name) AS last_name,
    TRIM(email) AS email
FROM users;

-- In WHERE clause (readable)
WHERE TRIM(status) = 'active';

-- Trim specific characters
SELECT TRIM('.' FROM file_extension) AS extension
FROM files;
-- '.txt.' → 'txt'

-- Trim specific characters from both sides
SELECT TRIM('[]' FROM '[data]') AS cleaned;
-- '[data]' → 'data'

-- Trim only leading
SELECT TRIM(LEADING FROM '  data  ') AS result;
-- '  data  ' → 'data  '

-- Trim only trailing
SELECT TRIM(TRAILING FROM '  data  ') AS result;
-- '  data  ' → '  data'

-- Trim specific character from one side
SELECT TRIM(LEADING '0' FROM '000123') AS result;
-- '000123' → '123'

-- When you need only one side (LTRIM/RTRIM still valid)
SELECT LTRIM(name) FROM users;  -- Only left trim needed
SELECT RTRIM(name) FROM users;  -- Only right trim needed

-- Complex trimming (SQL Server 2022+)
SELECT TRIM(',' FROM ',value1,value2,') AS result;
-- ',value1,value2,' → 'value1,value2'
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
    { "id": "prefer-trim-over-ltrim-rtrim", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
