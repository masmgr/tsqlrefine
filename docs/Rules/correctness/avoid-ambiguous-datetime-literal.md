# Avoid Ambiguous Datetime Literal

**Rule ID:** `avoid-ambiguous-datetime-literal`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Disallows slash-delimited date literals (e.g., `'12/31/2023'`, `'31/12/2023'`) as they depend on language and locale settings and can silently change meaning.

## Rationale

Slash-delimited date literals (`'12/31/2023'`, `'31/12/2023'`) are **locale-dependent** and cause silent data corruption or runtime errors.

**The problem: Same string, different dates**

```sql
-- US locale (us_english, dateformat mdy)
SELECT CAST('03/04/2023' AS DATE);  -- Returns 2023-03-04 (March 4th)

-- UK locale (British, dateformat dmy)
SELECT CAST('03/04/2023' AS DATE);  -- Returns 2023-04-03 (April 3rd)

-- Same query, different results in different environments!
```

**How SQL Server interprets date strings**:

SQL Server uses **SET LANGUAGE** and **SET DATEFORMAT** settings:

| Language | Default DATEFORMAT | '03/04/2023' Interpreted As |
|----------|-------------------|---------------------------|
| us_english | mdy (month/day/year) | March 4, 2023 |
| British | dmy (day/month/year) | April 3, 2023 |
| German | dmy | April 3, 2023 |
| French | dmy | April 3, 2023 |
| Japanese | ymd (year/month/day) | 2023-03-04 |

**Runtime errors with ambiguous dates**:

```sql
-- US locale (mdy)
SELECT CAST('31/12/2023' AS DATE);  -- Error: Month 31 doesn't exist!

-- Error message:
-- Msg 241, Level 16, State 1
-- Conversion failed when converting date and/or time from character string.
```

**Silent data corruption scenarios**:

1. **Development vs Production**: Different locale settings
   ```sql
   -- Development server (us_english, mdy)
   INSERT INTO Orders (OrderDate, Total) VALUES ('03/04/2023', 1000);
   -- Inserts March 4, 2023

   -- Production server (British, dmy)
   INSERT INTO Orders (OrderDate, Total) VALUES ('03/04/2023', 1000);
   -- Inserts April 3, 2023 (WRONG DATE!)
   ```

2. **Database migration**: Moving between regions
   ```sql
   -- Backup from US server, restore to UK server
   -- All date comparisons now use wrong interpretation
   SELECT * FROM Orders WHERE OrderDate < '01/06/2023';
   -- US: Orders before January 6
   -- UK: Orders before June 1 (different results!)
   ```

3. **SET LANGUAGE changes**: User session settings
   ```sql
   SET LANGUAGE British;
   SELECT * FROM Events WHERE EventDate = '03/04/2023';  -- April 3rd

   SET LANGUAGE us_english;
   SELECT * FROM Events WHERE EventDate = '03/04/2023';  -- March 4th
   -- Same query, different results!
   ```

**Why this is dangerous**:

1. **No compile-time error**: Query succeeds with wrong date
2. **No runtime warning**: Silent data corruption
3. **Environment-dependent**: Works in dev, fails in production
4. **Hard to debug**: Intermittent failures based on session settings
5. **Security risk**: Time-based access controls may fail

**Business impact**:

- **Financial reports**: Wrong date ranges (quarter/month-end reports)
- **Order processing**: Orders shipped on wrong dates
- **Compliance**: Audit logs with incorrect timestamps (GDPR, SOX)
- **Customer data**: Birthdates, subscription dates stored incorrectly
- **Service level**: SLA calculations based on wrong dates

**Unambiguous date formats** (work in all locales):

| Format | Example | Locale-Independent? | Notes |
|--------|---------|---------------------|-------|
| `YYYY-MM-DD` | `'2023-12-31'` | ✅ Yes | ISO 8601, **recommended** |
| `YYYYMMDD` | `'20231231'` | ✅ Yes | Compact, no separators |
| `YYYY-MM-DD HH:MM:SS` | `'2023-12-31 23:59:59'` | ✅ Yes | ISO 8601 with time |
| `MM/DD/YYYY` | `'12/31/2023'` | ❌ No | US locale only |
| `DD/MM/YYYY` | `'31/12/2023'` | ❌ No | UK/European locale |
| `YYYY/MM/DD` | `'2023/12/31'` | ⚠️ Depends | Works in most locales, but avoid |

**Best practice**: Always use ISO 8601 format (`YYYY-MM-DD`) in SQL code.

## Examples

### Bad

```sql
-- US format - ambiguous (mdy vs dmy)
SELECT * FROM Orders WHERE OrderDate = '12/31/2023';

-- Highly ambiguous - could be March 15 or December 3
SELECT * FROM Users WHERE CreatedAt > '3/15/2024';

-- European format - also ambiguous
SELECT * FROM Events WHERE EventDate = '31/12/2023';

-- Two-digit year - extremely ambiguous (century + locale)
SELECT * FROM Users WHERE CreatedAt > '1/1/23';  -- 1923? 2023?

-- Slash format in INSERT (dangerous!)
INSERT INTO Orders (OrderDate, Total) VALUES ('03/04/2023', 999.99);

-- Slash format in stored procedure (environment-dependent)
CREATE PROCEDURE GetOrdersByDate @StartDate VARCHAR(20)
AS
BEGIN
    SELECT * FROM Orders WHERE OrderDate >= @StartDate;
END;
EXEC GetOrdersByDate '01/06/2023';  -- January 6 or June 1?

-- Slash format in WHERE clause with BETWEEN
SELECT * FROM Sales
WHERE SaleDate BETWEEN '01/01/2023' AND '12/31/2023';

-- Slash format with time component
SELECT * FROM Logs WHERE LogTime = '03/15/2024 14:30:00';

-- Variable with slash format
DECLARE @CutoffDate VARCHAR(20) = '06/01/2023';
DELETE FROM TempData WHERE CreatedDate < @CutoffDate;

-- Dynamic SQL with slash format
DECLARE @SQL NVARCHAR(MAX);
SET @SQL = 'SELECT * FROM Orders WHERE OrderDate > ''01/15/2024''';
EXEC sp_executesql @SQL;
```

### Good

```sql
-- ISO 8601 format (YYYY-MM-DD) - unambiguous
SELECT * FROM Orders WHERE OrderDate = '2023-12-31';

-- ISO format for all comparisons
SELECT * FROM Users WHERE CreatedAt > '2024-03-15';

-- ISO format works in all locales
SELECT * FROM Events WHERE EventDate = '2023-12-31';

-- Four-digit year with ISO format
SELECT * FROM Users WHERE CreatedAt > '2023-01-01';

-- YYYYMMDD format (compact, unambiguous)
SELECT * FROM Logs WHERE LogDate = '20231231';

-- ISO format in INSERT
INSERT INTO Orders (OrderDate, Total) VALUES ('2023-03-04', 999.99);

-- ISO format in stored procedure
CREATE PROCEDURE GetOrdersByDate @StartDate DATE
AS
BEGIN
    SELECT * FROM Orders WHERE OrderDate >= @StartDate;
END;
EXEC GetOrdersByDate '2023-06-01';  -- Unambiguous

-- ISO format with BETWEEN
SELECT * FROM Sales
WHERE SaleDate BETWEEN '2023-01-01' AND '2023-12-31';

-- ISO format with time component
SELECT * FROM Logs WHERE LogTime = '2024-03-15 14:30:00';

-- Variable with ISO format
DECLARE @CutoffDate DATE = '2023-06-01';
DELETE FROM TempData WHERE CreatedDate < @CutoffDate;

-- Dynamic SQL with ISO format
DECLARE @SQL NVARCHAR(MAX);
SET @SQL = 'SELECT * FROM Orders WHERE OrderDate > ''2024-01-15''';
EXEC sp_executesql @SQL;

-- CONVERT with explicit style (unambiguous)
SELECT * FROM Orders
WHERE OrderDate = CONVERT(DATE, '2023-12-31', 23);  -- Style 23: ISO format

-- DATEFROMPARTS function (completely unambiguous)
SELECT * FROM Orders
WHERE OrderDate = DATEFROMPARTS(2023, 12, 31);  -- Year, Month, Day

-- Date literals with explicit DATE type
SELECT * FROM Events
WHERE EventDate = CAST('2024-03-15' AS DATE);

-- DATEADD for relative dates (no literal strings)
SELECT * FROM Orders
WHERE OrderDate > DATEADD(DAY, -30, GETDATE());

-- Using variables with proper types
DECLARE @StartDate DATE = '2023-01-01';
DECLARE @EndDate DATE = '2023-12-31';
SELECT * FROM Sales WHERE SaleDate BETWEEN @StartDate AND @EndDate;
```

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "avoid-ambiguous-datetime-literal", "enabled": false }
  ]
}
```

## See Also

- [avoid-magic-convert-style-for-datetime](../style/avoid-magic-convert-style-for-datetime.md) - Warns on datetime CONVERT style numbers
- [prefer-utc-datetime](../performance/prefer-utc-datetime.md) - Detects local datetime functions and suggests UTC alternatives
