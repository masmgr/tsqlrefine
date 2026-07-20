# Deep View Nesting

**Rule ID:** `deep-view-nesting`  
**Category:** Performance  
**Severity:** Warning  
**Fixable:** No

Detects view dependency chains deeper than `max`, which defaults to 3. The depth is the number of
view-to-view dependency edges below the reported view. Cycles are handled separately by
`circular-object-reference`.

```sql
CREATE VIEW dbo.SummaryView AS
SELECT Id FROM dbo.IntermediateView;
```

```json
{
  "rules": {
    "deep-view-nesting": {
      "severity": "warning",
      "options": { "max": 3 }
    }
  }
}
```

The `max` option accepts an integer from 1 to 10000.
