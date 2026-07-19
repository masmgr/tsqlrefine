# Maximum Parameter Count

**Rule ID:** `max-parameter-count`  
**Category:** Performance  
**Severity:** Information  
**Fixable:** No

Limits the number of declared parameters on each procedure or function. Views, triggers, and
standalone batches have a parameter count of zero.

## Configuration

The `max` option is an integer from 1 to 10000. Its default is 15.

```json
{
  "rules": {
    "max-parameter-count": {
      "severity": "info",
      "options": { "max": 15 }
    }
  }
}
```
