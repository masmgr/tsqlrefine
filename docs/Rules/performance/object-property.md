# Object Property

**Rule ID:** `object-property`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Prohibit OBJECTPROPERTY function; use OBJECTPROPERTYEX or sys catalog views instead

## Rationale

`OBJECTPROPERTY` is **deprecated** and has been replaced by better alternatives:

1. **Microsoft deprecation**: `OBJECTPROPERTY` is documented as deprecated since SQL Server 2012 and may be removed in future versions

2. **Limited property support**: `OBJECTPROPERTY` returns fewer properties than `OBJECTPROPERTYEX`
   - Missing newer object metadata introduced after SQL Server 2000
   - Returns NULL for properties it doesn't recognize

3. **Performance**: System catalog views (`sys.objects`, `sys.tables`, etc.) are faster and more flexible
   - Can retrieve multiple properties in a single query
   - Better query optimization with joins
   - Type-safe results (no string property names)

4. **Future compatibility**: Code using `OBJECTPROPERTY` may break in future SQL Server versions

**Better alternatives:**
- `OBJECTPROPERTYEX()`: Extended version with more properties
- `sys.objects`: Catalog view with all object metadata
- `sys.tables`, `sys.procedures`, etc.: Type-specific catalog views

## Examples

### Bad

```sql
-- Deprecated function
SELECT OBJECTPROPERTY(OBJECT_ID('dbo.Users'), 'TableHasPrimaryKey');

-- Multiple calls for multiple properties (inefficient)
SELECT OBJECTPROPERTY(OBJECT_ID('dbo.Users'), 'TableHasPrimaryKey'),
       OBJECTPROPERTY(OBJECT_ID('dbo.Users'), 'TableHasIndex'),
       OBJECTPROPERTY(OBJECT_ID('dbo.Users'), 'TableHasClustIndex');

-- May return NULL for newer properties
SELECT OBJECTPROPERTY(OBJECT_ID('dbo.Users'), 'IsMSShipped');
```

### Good

```sql
-- Use OBJECTPROPERTYEX for single property checks
SELECT OBJECTPROPERTYEX(OBJECT_ID('dbo.Users'), 'BaseType');

-- Better: Use sys.objects catalog view
SELECT name, type_desc, create_date, modify_date
FROM sys.objects
WHERE name = 'Users' AND schema_id = SCHEMA_ID('dbo');

-- Check for primary key using catalog views
SELECT OBJECT_NAME(i.object_id) AS table_name,
       i.name AS constraint_name
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('dbo.Users')
  AND i.is_primary_key = 1;

-- Get multiple table properties efficiently
SELECT t.name,
       t.create_date,
       t.modify_date,
       p.rows AS row_count,
       i.name AS primary_key_name
FROM sys.tables t
LEFT JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
LEFT JOIN sys.indexes i ON t.object_id = i.object_id AND i.is_primary_key = 1
WHERE t.name = 'Users';

-- Check if table is system table
SELECT name, is_ms_shipped
FROM sys.objects
WHERE object_id = OBJECT_ID('dbo.Users');
```

## Configuration

To disable this rule, add it to your `tsqlrefine.json`:

```json
{
  "ruleset": "custom-ruleset.json"
}
```

In `custom-ruleset.json`:

```json
{
  "rules": [
    { "id": "object-property", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
