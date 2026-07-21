# Prefer EOMONTH Over Date Arithmetic

**Rule ID:** `prefer-eomonth-over-date-arithmetic`
**Category:** Style
**Severity:** Information
**Fixable:** No

Recommends `EOMONTH` over the common nested `DATEADD` month-end calculation.

```sql
-- Bad
SELECT DATEADD(day, -1,
    DATEADD(month, 1,
        DATEADD(month, DATEDIFF(month, 0, @date), 0)));

-- Good
SELECT EOMONTH(@date);
```
