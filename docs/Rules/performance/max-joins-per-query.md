# Maximum Joins per Query

**Rule ID:** `max-joins-per-query`  
**Category:** Performance  
**Severity:** Warning  
**Fixable:** No

Limits the largest number of joins in any single query within a SQL object or batch. Queries in
nested subqueries are measured independently.

## Configuration

The `max` option is an integer from 1 to 10000. Its default is 8.

```json
{
  "rules": {
    "max-joins-per-query": {
      "severity": "warning",
      "options": { "max": 8 }
    }
  }
}
```
