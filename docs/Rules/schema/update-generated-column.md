# update-generated-column

## Summary

Reports `UPDATE` assignments to computed or identity columns.

## Examples

```sql
UPDATE dbo.Users SET Id = 2 WHERE Id = 1;
UPDATE dbo.Users SET ComputedDisplayName = N'User' WHERE Id = 1;
```

## Detection

The rule resolves direct and `UPDATE ... FROM` alias targets through the schema snapshot.
Views, temporary tables, table variables, and unresolved targets are skipped.

## Category

Schema

## Severity

Error

## Fixable

No
