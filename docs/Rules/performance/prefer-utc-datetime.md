# Prefer Utc Datetime

**Rule ID:** `prefer-utc-datetime`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Detects local datetime functions (GETDATE, SYSDATETIME, CURRENT_TIMESTAMP, SYSDATETIMEOFFSET) and suggests UTC alternatives for consistency across time zones

## Rationale

Using local datetime functions (`GETDATE()`, `SYSDATETIME()`, `CURRENT_TIMESTAMP`) instead of UTC alternatives can cause **timezone-related data integrity and consistency issues**:

1. **Daylight Saving Time (DST) problems**:
   - Duplicate timestamps during "fall back" (2:00 AM → 1:00 AM)
   - Missing timestamps during "spring forward" (2:00 AM → 3:00 AM)
   - Ambiguous time ranges (is 1:30 AM before or after the clock change?)

2. **Multi-region applications**:
   - Different server timezones produce different timestamps
   - Difficult to compare timestamps across regions
   - Reporting and analytics become complex

3. **Server relocation issues**:
   - Moving database to a different timezone changes timestamp semantics
   - Historical data becomes inconsistent with new data

4. **Business logic errors**:
   - "Yesterday's sales" may include different time ranges depending on timezone
   - SLA calculations may be incorrect near DST transitions

**When UTC is appropriate:**
- Storing event timestamps (orders, logs, user actions)
- Multi-region applications
- Audit trails and compliance
- Distributed systems

**When local time is acceptable:**
- Display-only timestamps (convert UTC to local for display)
- Single-region applications with no DST concerns
- Business hours calculations for a specific office

## Examples

### Bad

```sql
-- Local time (server timezone-dependent)
INSERT INTO orders (created_at)
VALUES (GETDATE());  -- 2024-03-10 02:30:00 (ambiguous during DST)

-- CURRENT_TIMESTAMP (same as GETDATE)
UPDATE users
SET last_login = CURRENT_TIMESTAMP;

-- SYSDATETIME (high precision local time)
INSERT INTO audit_log (event_time)
VALUES (SYSDATETIME());  -- Still server timezone

-- SYSDATETIMEOFFSET includes timezone offset, but not recommended for storage
SELECT SYSDATETIMEOFFSET();  -- 2024-03-10 02:30:00.000 -05:00
```

### Good

```sql
-- UTC time (consistent across timezones)
INSERT INTO orders (created_at)
VALUES (GETUTCDATE());  -- 2024-03-10 07:30:00 (no ambiguity)

-- High precision UTC time
INSERT INTO audit_log (event_time)
VALUES (SYSUTCDATETIME());

-- Store UTC, convert to local for display
SELECT order_id,
       created_at AS created_utc,
       created_at AT TIME ZONE 'UTC' AT TIME ZONE 'Eastern Standard Time' AS created_est
FROM orders;

-- For SQL Server 2016+ with DATETIMEOFFSET
INSERT INTO events (event_time)
VALUES (SYSUTCDATETIME() AT TIME ZONE 'UTC');

-- Calculate time ranges using UTC
SELECT *
FROM orders
WHERE created_at >= DATEADD(DAY, -1, GETUTCDATE())
  AND created_at < GETUTCDATE();

-- Exception: Local time for business hours (single location)
DECLARE @BusinessHoursStart TIME = '09:00:00';
DECLARE @BusinessHoursEnd TIME = '17:00:00';

SELECT *
FROM support_tickets
WHERE CAST(GETDATE() AS TIME) BETWEEN @BusinessHoursStart AND @BusinessHoursEnd;
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
    { "id": "prefer-utc-datetime", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
