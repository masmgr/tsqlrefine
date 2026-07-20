# Maximum Cyclomatic Complexity

**Rule ID:** `max-cyclomatic-complexity`  
**Category:** Performance  
**Severity:** Warning  
**Fixable:** No

Limits cyclomatic complexity for each procedure, function, view, trigger, or standalone batch.
Complexity is one plus the number of `IF`, `WHILE`, and searched `CASE WHEN` decision points.

## Example

With `max` set to `1`, this procedure is reported because its complexity is 2:

```sql
CREATE PROCEDURE dbo.UpdateStatus @enabled bit
AS
BEGIN
    IF @enabled = 1
        UPDATE dbo.Settings SET IsEnabled = 1;
END;
```

## Configuration

The `max` option is an integer from 1 to 10000. Its default is 20.

```json
{
  "rules": {
    "max-cyclomatic-complexity": {
      "severity": "warning",
      "options": { "max": 20 }
    }
  }
}
```

Split objects with excessive branching into smaller routines with focused responsibilities.
