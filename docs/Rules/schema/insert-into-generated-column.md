# insert-into-generated-column

## Summary

Reports explicit `INSERT` writes to computed columns and to identity columns when
`IDENTITY_INSERT` has not been enabled for the target table earlier in the script.

## Examples

```sql
INSERT dbo.Users (ComputedDisplayName) VALUES (N'User');
INSERT dbo.Users (Id, Name) VALUES (1, N'User');
```

```sql
SET IDENTITY_INSERT dbo.Users ON;
INSERT dbo.Users (Id, Name) VALUES (1, N'User');
SET IDENTITY_INSERT dbo.Users OFF;
```

## Detection

The rule requires a schema snapshot and an explicit INSERT column list. Views, temporary
tables, table variables, and unresolved targets are skipped. `IDENTITY_INSERT` is tracked
in source order within each procedure or top-level script scope; dynamically executed or
externally established session state is not known.

## Category

Schema

## Severity

Error

## Fixable

No
