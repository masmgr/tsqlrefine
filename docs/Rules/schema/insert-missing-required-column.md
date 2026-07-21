# insert-missing-required-column

## Summary

Reports an `INSERT` with an explicit column list, or `DEFAULT VALUES`, that omits a
non-nullable column without a default or generated value.

## Examples

```sql
-- dbo.Users.Name is NOT NULL and has no default.
INSERT dbo.Users (Email) VALUES (N'user@example.com');
```

```sql
INSERT dbo.Users (Name, Email) VALUES (N'User', N'user@example.com');
```

## Detection

The rule requires a native schema snapshot. Nullable, identity, computed, rowversion,
timestamp, and default-backed columns do not require an explicit value. Views, temporary
tables, table variables, unresolved targets, and inserts without a column list are skipped.

## Category

Schema

## Severity

Error

## Fixable

No
