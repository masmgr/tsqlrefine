# Duplicate Empty Line

**Rule ID:** `duplicate-empty-line`
**Category:** Style
**Severity:** Information
**Fixable:** No

## Description

Avoid consecutive empty lines (more than one blank line in a row).

## Rationale

Multiple consecutive empty lines (3+ blank lines) create **excessive whitespace** that reduces code density without improving readability:

1. **Wasted screen space**: More scrolling required to view code
   - Forces developers to scroll more to see logical sections
   - Reduces context visible on screen at once

2. **Inconsistent formatting**: Different developers use different amounts of whitespace
   - Some use 2 blank lines, others use 5
   - Creates visual inconsistency across codebase

3. **No semantic value**: Single blank line already provides visual separation
   - Additional blank lines don't add clarity
   - May indicate accidental copy-paste or editing errors

**Best practice**: Use **one blank line** to separate logical sections (procedure, statement groups, comments).

## Examples

### Bad

```sql
-- Excessive whitespace (3+ consecutive empty lines)
SELECT 1;



SELECT 2;

-- Scattered excessive spacing
CREATE PROCEDURE dbo.GetUser AS
BEGIN
    SELECT * FROM users;



    SELECT * FROM profiles;
END;

-- Inconsistent spacing (makes code look unpolished)
DECLARE @x INT = 1;


DECLARE @y INT = 2;



DECLARE @z INT = 3;
```

### Good

```sql
-- Single blank line for separation
SELECT 1;

SELECT 2;

-- Consistent spacing in procedures
CREATE PROCEDURE dbo.GetUser AS
BEGIN
    SELECT * FROM users;

    SELECT * FROM profiles;
END;

-- Consistent single blank lines
DECLARE @x INT = 1;

DECLARE @y INT = 2;

DECLARE @z INT = 3;

-- Multiple related statements (no blank lines needed)
DECLARE @StartDate DATE = '2024-01-01';
DECLARE @EndDate DATE = '2024-12-31';
DECLARE @Status NVARCHAR(20) = 'active';

-- Blank line before new logical section
SELECT * FROM orders
WHERE order_date BETWEEN @StartDate AND @EndDate
  AND status = @Status;
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
    { "id": "duplicate-empty-line", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
