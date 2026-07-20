# Maximum Nesting Depth

**Rule ID:** `max-nesting-depth`  
**Category:** Performance  
**Severity:** Warning  
**Fixable:** No

Limits the maximum nesting of `BEGIN...END`, `IF`, `WHILE`, and `TRY...CATCH` constructs in each
SQL object or batch. Deep nesting makes control flow difficult to review and test.

## Example

With `max` set to `1`, this nested control flow is reported:

```sql
IF EXISTS (SELECT 1 FROM dbo.Tasks)
BEGIN
    WHILE EXISTS (SELECT 1 FROM dbo.Tasks WHERE IsPending = 1)
        UPDATE TOP (1) dbo.Tasks SET IsPending = 0 WHERE IsPending = 1;
END;
```

## Configuration

The `max` option is an integer from 1 to 10000. Its default is 5.

```json
{
  "rules": {
    "max-nesting-depth": {
      "severity": "warning",
      "options": { "max": 5 }
    }
  }
}
```
