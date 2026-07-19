# Maximum Statement Count

**Rule ID:** `max-statement-count`  
**Category:** Performance  
**Severity:** Information  
**Fixable:** No

Limits the number of executable statements in each SQL object or standalone batch. Container and
control-flow statements are included because they contribute to the size of the executable unit.

## Example

With `max` set to `2`, this batch is reported because it contains three statements:

```sql
SELECT 1;
SELECT 2;
SELECT 3;
```

## Configuration

The `max` option is an integer from 1 to 10000. Its default is 200.

```json
{
  "rules": {
    "max-statement-count": {
      "severity": "info",
      "options": { "max": 200 }
    }
  }
}
```
