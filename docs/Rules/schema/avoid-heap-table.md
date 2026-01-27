# Avoid Heap Table

**Rule ID:** `avoid-heap-table`
**Category:** Schema Design
**Severity:** Warning
**Fixable:** No

## Description

Warns when tables are created as heaps (without a clustered index). Heaps can lead to unpredictable performance and increased maintenance costs.

## Rationale

A heap is a table without a clustered index. While heaps have some valid use cases, they generally have significant disadvantages:

**Performance issues:**
- **No physical ordering**: Data is stored in no particular order, making range scans inefficient
- **Forwarding pointers**: Updates that increase row size create forwarding pointers, degrading performance
- **Poor scan performance**: Full table scans on heaps are often slower than on clustered indexes
- **Inefficient space usage**: Fragmentation can waste significant storage space

**Maintenance problems:**
- **Difficult to optimize**: Can't be reorganized like indexes; requires rebuilding
- **Index bloat**: Non-clustered indexes on heaps include RID (Row ID) which is larger than a clustered key
- **Statistics issues**: Harder to maintain accurate statistics without a clustered index

**Best practice:** Every table should have a clustered index, typically on the primary key or the most frequently queried column(s).

## Examples

### Bad

```sql
-- Table without any index - stored as a heap
CREATE TABLE users (
    id INT,
    name VARCHAR(50)
);

-- Table with non-clustered index only - still a heap
CREATE TABLE logs (
    id INT,
    message VARCHAR(MAX),
    INDEX IX_Logs NONCLUSTERED (id)
);

-- Primary key but explicitly non-clustered - still a heap
CREATE TABLE orders (
    order_id INT PRIMARY KEY NONCLUSTERED,
    customer_id INT,
    order_date DATE
);
```

### Good

```sql
-- Clustered primary key on column
CREATE TABLE users (
    id INT PRIMARY KEY CLUSTERED,
    name VARCHAR(50)
);

-- Clustered primary key as table constraint
CREATE TABLE products (
    id INT,
    name VARCHAR(50),
    CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (id)
);

-- Explicit clustered index
CREATE TABLE orders (
    id INT,
    date DATE,
    INDEX IX_Orders CLUSTERED (id)
);

-- Clustered columnstore index (for analytical workloads)
CREATE TABLE events (
    id INT,
    timestamp DATETIME,
    data VARCHAR(MAX),
    INDEX IX_Events CLUSTERED COLUMNSTORE
);

-- Composite clustered primary key
CREATE TABLE order_items (
    order_id INT NOT NULL,
    line_number INT NOT NULL,
    product_id INT,
    CONSTRAINT PK_OrderItems PRIMARY KEY CLUSTERED (order_id, line_number)
);

-- Temporary tables (automatically excluded from this rule)
CREATE TABLE #temp_staging (
    id INT,
    data VARCHAR(100)
);

CREATE TABLE ##global_temp (
    id INT,
    value DECIMAL(10,2)
);
```

## Exclusions

This rule does **not** apply to:
- **Temporary tables**: Local temporary tables (`#temp`) and global temporary tables (`##temp`) are automatically excluded from this rule, as they are typically used for short-lived staging scenarios where heap storage is acceptable.

## Valid Use Cases for Heaps

In rare cases, heaps may be appropriate for permanent tables:
- **Staging tables**: Tables for bulk loading where data will be immediately processed and deleted
- **Very small tables**: Tables with only a few rows where index overhead exceeds benefits
- **Write-heavy workloads**: Tables with extremely high insert rates and no queries (rare)

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "avoid-heap-table", "enabled": false }
  ]
}
```

## See Also

- [require-primary-key-or-unique-constraint](require-primary-key-or-unique-constraint.md) - Requires PRIMARY KEY or UNIQUE constraints
