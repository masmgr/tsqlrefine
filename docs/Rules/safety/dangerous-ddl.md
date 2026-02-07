# Dangerous DDL

**Rule ID:** `dangerous-ddl`
**Category:** Safety
**Severity:** Warning (default), Information (when IF EXISTS is used)
**Fixable:** No

## Description

Detects destructive DDL operations (DROP, TRUNCATE, ALTER TABLE DROP) that can cause irreversible data loss in production.

## Rationale

These DDL operations are **one-way** (no rollback) and commonly cause production incidents when executed accidentally:

- **DROP DATABASE** - Catastrophic data loss requiring restore from backup
- **DROP TABLE/VIEW/PROCEDURE/FUNCTION** - Object and data loss, breaks dependent code
- **TRUNCATE TABLE** - All rows deleted, minimal logging (harder to recover)
- **ALTER TABLE DROP COLUMN** - Data loss, breaks queries/procedures
- **ALTER TABLE DROP CONSTRAINT** - Referential integrity loss, can allow bad data

Even with backups, recovery causes downtime and potential data loss (transactions since last backup).

## Examples

### Bad

```sql
-- WARNING: Catastrophic data loss
DROP DATABASE Production_DB;

-- WARNING: Table-level data loss
DROP TABLE dbo.Orders;

-- INFORMATION: IF EXISTS lowers severity, but still flagged
DROP TABLE IF EXISTS dbo.Customers;

-- WARNING: All data removed
TRUNCATE TABLE dbo.Logs;

-- WARNING: Data loss for existing rows
ALTER TABLE dbo.Users DROP COLUMN EmailAddress;

-- WARNING: Referential integrity removed
ALTER TABLE dbo.Orders DROP CONSTRAINT FK_Orders_Customers;
```

### Good

```sql
-- Use DROP DATABASE only in non-production or with explicit safeguards
-- (Consider disabling this rule for deployment scripts)

-- For data cleanup, use DELETE with WHERE
DELETE FROM dbo.Logs WHERE LogDate < DATEADD(day, -30, GETDATE());

-- For schema changes, plan migrations carefully
-- Step 1: Add new column
ALTER TABLE dbo.Users ADD Email NVARCHAR(255) NULL;

-- Step 2: Migrate data
UPDATE dbo.Users SET Email = EmailAddress WHERE Email IS NULL;

-- Step 3: Drop old column (after verification)
-- ALTER TABLE dbo.Users DROP COLUMN EmailAddress;
```

## Severity Behavior

The severity is lowered from **Warning** to **Information** when `IF EXISTS` is used on DROP TABLE, DROP VIEW, DROP PROCEDURE, or DROP FUNCTION statements. This recognizes that `IF EXISTS` indicates an intentional, idempotent deployment pattern rather than an accidental destructive operation.

| Statement | Without IF EXISTS | With IF EXISTS |
|-----------|-------------------|----------------|
| `DROP TABLE` | Warning | Information |
| `DROP VIEW` | Warning | Information |
| `DROP PROCEDURE` | Warning | Information |
| `DROP FUNCTION` | Warning | Information |
| `DROP DATABASE` | Warning | — (no IF EXISTS support) |
| `TRUNCATE TABLE` | Warning | — (no IF EXISTS support) |
| `ALTER TABLE DROP` | Warning | — (no IF EXISTS support) |

## Common Patterns

### Temp Tables

Temp tables are excluded from warnings:

```sql
-- OK: Temp table cleanup
DROP TABLE #TempResults;
DROP TABLE ##GlobalTemp;
```

### Deployment Scripts

For deployment/migration scripts, you may want to disable this rule:

```sql
-- .tsqlrefine-ignore dangerous-ddl
DROP TABLE IF EXISTS dbo.ObsoleteTable;
ALTER TABLE dbo.Users DROP CONSTRAINT CK_Users_LegacyCheck;
```

Or use inline comments:

```sql
DROP TABLE dbo.ObsoleteTable;  -- tsqlrefine-disable-line dangerous-ddl
```

### Safe Alternatives

| Dangerous Operation | Safer Alternative |
|---------------------|-------------------|
| `DROP TABLE` | Rename table, keep for rollback: `sp_rename 'dbo.Orders', 'Orders_OLD'` |
| `TRUNCATE TABLE` | `DELETE FROM table WHERE <condition>` (allows rollback) |
| `DROP COLUMN` | 3-step migration: add new, migrate data, drop old |
| `DROP CONSTRAINT` | Disable constraint first, test, then drop if needed |

## Configuration

To disable this rule (e.g., for deployment scripts):

```json
{
  "ruleset": "custom-ruleset.json"
}
```

In `custom-ruleset.json`:

```json
{
  "rules": [
    { "id": "dangerous-ddl", "enabled": false }
  ]
}
```

## Limitations

- **Temp tables** (`#` and `##`) are excluded from warnings
- Cannot detect **context** (production vs. development environment)
- **Dynamic SQL** containing DDL is not analyzed

## See Also

- [require-drop-if-exists](require-drop-if-exists.md) - Requires IF EXISTS on DROP statements
- [dml-without-where](dml-without-where.md) - Related rule for destructive DML
- [TsqlRefine Rules Documentation](../README.md)
