# Maximum Joins per Query

**Rule ID:** `max-joins-per-query`  
**Category:** Performance  
**Severity:** Warning  
**Fixable:** No

Limits the largest number of joins in any single query within a SQL object or batch. Queries in
nested subqueries are measured independently.

## Example

With `max` set to `1`, this query is reported because it contains two joins:

```sql
SELECT o.OrderId
FROM dbo.Orders AS o
JOIN dbo.Customers AS c ON c.CustomerId = o.CustomerId
JOIN dbo.Regions AS r ON r.RegionId = c.RegionId;
```

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
