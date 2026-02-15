# Avoid Information Schema

**Rule ID:** `avoid-information-schema`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Prohibit INFORMATION_SCHEMA views; use sys catalog views for better performance

## Rationale

`INFORMATION_SCHEMA` views are **ANSI standard** views for metadata, but SQL Server's **native `sys` catalog views** offer significant advantages:

1. **Performance**:
   - `INFORMATION_SCHEMA` views have additional abstraction layers and joins
   - `sys` catalog views query underlying system tables directly
   - Benchmark: `sys.tables` is 2-10x faster than `INFORMATION_SCHEMA.TABLES`

2. **More complete metadata**:
   - `INFORMATION_SCHEMA` provides only ANSI-standard metadata (limited subset)
   - `sys` catalog views expose SQL Server-specific features:
     - Partitioning information
     - Compression settings
     - Filestream and FileTable metadata
     - Extended properties
     - Service Broker objects

3. **Better type information**:
   - `sys` views use native SQL Server data types
   - `INFORMATION_SCHEMA` converts types to ANSI standard names (lossy conversion)

4. **Consistency**: All SQL Server documentation and examples use `sys` views

**When `INFORMATION_SCHEMA` might be acceptable:**
- Cross-platform applications targeting multiple databases (MySQL, PostgreSQL)
- Legacy code already using `INFORMATION_SCHEMA`
- Simple metadata queries where performance is not critical

**Recommendation**: Use `sys` catalog views for SQL Server-specific code.

## Examples

### Bad

```sql
-- INFORMATION_SCHEMA.TABLES (slower, limited columns)
SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE';

-- INFORMATION_SCHEMA.COLUMNS (missing SQL Server-specific metadata)
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Users';

-- INFORMATION_SCHEMA.ROUTINES (no extended properties)
SELECT ROUTINE_NAME, ROUTINE_TYPE
FROM INFORMATION_SCHEMA.ROUTINES
WHERE ROUTINE_SCHEMA = 'dbo';

-- INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE (verbose joins)
SELECT tc.CONSTRAINT_NAME, tc.TABLE_NAME, ccu.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
INNER JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE ccu
    ON tc.CONSTRAINT_NAME = ccu.CONSTRAINT_NAME
WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY';
```

### Good

```sql
-- sys.tables (faster, more columns)
SELECT SCHEMA_NAME(schema_id) AS schema_name,
       name AS table_name,
       type_desc,
       create_date,
       modify_date,
       is_memory_optimized  -- Not available in INFORMATION_SCHEMA
FROM sys.tables;

-- sys.columns (includes SQL Server-specific metadata)
SELECT c.name AS column_name,
       TYPE_NAME(c.user_type_id) AS data_type,
       c.max_length,
       c.precision,
       c.scale,
       c.is_nullable,
       c.is_identity,           -- Not in INFORMATION_SCHEMA
       c.is_computed,           -- Not in INFORMATION_SCHEMA
       c.is_sparse              -- Not in INFORMATION_SCHEMA
FROM sys.columns c
INNER JOIN sys.tables t ON c.object_id = t.object_id
WHERE t.name = 'Users';

-- sys.procedures and sys.sql_modules (with definition)
SELECT p.name AS procedure_name,
       p.create_date,
       p.modify_date,
       m.definition  -- Full procedure definition
FROM sys.procedures p
LEFT JOIN sys.sql_modules m ON p.object_id = m.object_id
WHERE SCHEMA_NAME(p.schema_id) = 'dbo';

-- sys.key_constraints and sys.index_columns (cleaner join)
SELECT i.name AS constraint_name,
       OBJECT_NAME(ic.object_id) AS table_name,
       COL_NAME(ic.object_id, ic.column_id) AS column_name,
       ic.key_ordinal  -- Column order in key
FROM sys.key_constraints kc
INNER JOIN sys.indexes i ON kc.parent_object_id = i.object_id
    AND kc.unique_index_id = i.index_id
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id
    AND i.index_id = ic.index_id
WHERE kc.type = 'PK';

-- Advanced: Get table size and compression (impossible with INFORMATION_SCHEMA)
SELECT t.name AS table_name,
       p.rows,
       SUM(a.total_pages) * 8 AS total_space_kb,
       SUM(a.used_pages) * 8 AS used_space_kb,
       p.data_compression_desc  -- ROW, PAGE, NONE
FROM sys.tables t
INNER JOIN sys.indexes i ON t.object_id = i.object_id
INNER JOIN sys.partitions p ON i.object_id = p.object_id
    AND i.index_id = p.index_id
INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
GROUP BY t.name, p.rows, p.data_compression_desc;
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
    { "id": "avoid-information-schema", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
