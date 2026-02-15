# Require Data Compression

**Rule ID:** `require-data-compression`
**Category:** Performance
**Severity:** Information
**Fixable:** No

## Description

Recommends specifying DATA_COMPRESSION option in CREATE TABLE for storage and performance optimization.

## Rationale

Data compression reduces storage costs and can improve query performance through reduced I/O.

**Benefits of data compression**:

1. **Reduced storage**: Up to 50-90% storage reduction for typical tables
2. **Improved I/O performance**: Less data to read from disk
3. **Better memory utilization**: More rows fit in buffer pool
4. **Lower backup costs**: Smaller backup files and faster backups

**Compression types**:

- **ROW compression**: Stores fixed-length data types more efficiently
- **PAGE compression**: Includes ROW compression plus column prefix and dictionary compression
- **COLUMNSTORE compression**: For columnar storage (analytical workloads)

**Trade-offs**:

- CPU overhead for compression/decompression (usually negligible)
- Not beneficial for small tables or already-compressed data
- Requires SQL Server 2008+ Enterprise Edition (SQL Server 2016 SP1+ supports in Standard Edition)

## Examples

### Bad

```sql
-- No compression specified (defaults to NONE)
CREATE TABLE dbo.Users (
    Id INT PRIMARY KEY,
    Name VARCHAR(100),
    Email VARCHAR(100),
    CreatedDate DATETIME
);

-- Large fact table without compression
CREATE TABLE dbo.OrderDetails (
    OrderId INT,
    ProductId INT,
    Quantity INT,
    Price DECIMAL(10,2)
);
```

### Good

```sql
-- ROW compression for moderate space savings
CREATE TABLE dbo.Users (
    Id INT PRIMARY KEY,
    Name VARCHAR(100),
    Email VARCHAR(100),
    CreatedDate DATETIME
)
WITH (DATA_COMPRESSION = ROW);

-- PAGE compression for maximum space savings (best for large tables)
CREATE TABLE dbo.OrderDetails (
    OrderId INT,
    ProductId INT,
    Quantity INT,
    Price DECIMAL(10,2)
)
WITH (DATA_COMPRESSION = PAGE);

-- Per-partition compression (different compression per partition)
CREATE TABLE dbo.SalesHistory (
    SaleId INT,
    SaleDate DATE,
    Amount DECIMAL(10,2)
)
ON SalesPartitionScheme(SaleDate)
WITH (
    DATA_COMPRESSION = PAGE ON PARTITIONS (1 TO 10),
    DATA_COMPRESSION = ROW ON PARTITIONS (11 TO 12)
);
```

**When to use compression**:

- Large tables (>1GB) with repetitive data
- Read-heavy workloads (data warehouses, reporting)
- Tables with many NULL values or fixed-length columns
- Tables with low write frequency

**When to skip compression**:

- Small tables (<100MB)
- Write-heavy OLTP workloads with tight CPU constraints
- Already-compressed data (images, encrypted data)

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
    { "id": "require-data-compression", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
