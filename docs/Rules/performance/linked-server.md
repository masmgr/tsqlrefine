# Linked Server

**Rule ID:** `linked-server`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Prohibit linked server queries (4-part identifiers); use alternative data access patterns

## Rationale

Linked server queries using 4-part identifiers (`[Server].[Database].[Schema].[Table]`) have **severe performance and reliability issues**:

1. **Massive data transfer**:
   - Entire remote table may be transferred to local server for filtering/joining
   - No predicate pushdown optimization in many cases
   - Network latency compounds the problem
   - Example: `WHERE` clause on remote table may fetch all rows, then filter locally

2. **Blocking and locking**:
   - Queries hold locks on remote server for entire execution duration
   - Distributed transactions require MS DTC (often disabled for security)
   - Transaction isolation level mismatches cause deadlocks

3. **Reliability issues**:
   - Network failures break queries mid-execution
   - Authentication and permission synchronization complexity
   - Linked server configuration drift between environments
   - Difficult to troubleshoot (which server is slow?)

4. **Security concerns**:
   - Credentials stored in linked server configuration
   - Difficult to audit cross-server access
   - Increased attack surface

**Better alternatives:**
- **Denormalize data**: Replicate needed data locally (ETL/replication)
- **Microservices architecture**: Move logic to application layer
- **Message queue**: Asynchronous data synchronization
- **OPENROWSET**: Ad-hoc queries with explicit connection strings (more visible)
- **API calls**: Application code calls remote service and joins in-memory

## Examples

### Bad

```sql
-- 4-part identifier (entire remote table may be transferred)
SELECT u.username, o.order_date
FROM [RemoteServer].[RemoteDB].[dbo].[Users] u
INNER JOIN [LocalDB].[dbo].[Orders] o ON u.user_id = o.user_id
WHERE o.order_date > '2024-01-01';  -- Filter may not push down to remote

-- Aggregation on linked server (huge data transfer)
SELECT COUNT(*)
FROM [RemoteServer].[SalesDB].[dbo].[SalesHistory]
WHERE sale_date > DATEADD(YEAR, -1, GETDATE());  -- All rows fetched, then counted locally

-- UPDATE via linked server (distributed transaction)
UPDATE [RemoteServer].[RemoteDB].[dbo].[Users]
SET status = 'inactive'
WHERE last_login < DATEADD(YEAR, -1, GETDATE());  -- Requires MS DTC

-- JOIN multiple linked servers (network amplification)
SELECT *
FROM [Server1].[DB1].[dbo].[Customers] c
INNER JOIN [Server2].[DB2].[dbo].[Orders] o ON c.customer_id = o.customer_id
INNER JOIN [Server3].[DB3].[dbo].[Products] p ON o.product_id = p.product_id;
-- All data transferred to local server for joining!
```

### Good

```sql
-- Option 1: Replicate data locally (best for frequent access)
-- Use SQL Server Replication, Change Data Capture, or ETL
SELECT u.username, o.order_date
FROM LocalDB.dbo.Users_Replica u  -- Replicated from remote
INNER JOIN LocalDB.dbo.Orders o ON u.user_id = o.user_id
WHERE o.order_date > '2024-01-01';

-- Option 2: OPENROWSET for ad-hoc queries (more explicit)
SELECT u.username, o.order_date
FROM OPENROWSET(
    'SQLNCLI',
    'Server=RemoteServer;Database=RemoteDB;Trusted_Connection=yes;',
    'SELECT user_id, username FROM dbo.Users WHERE status = ''active'''
) u
INNER JOIN LocalDB.dbo.Orders o ON u.user_id = o.user_id
WHERE o.order_date > '2024-01-01';

-- Option 3: Application-layer join (fetch from both servers, join in code)
-- C# pseudocode:
-- var users = remoteDbContext.Users.Where(u => u.Status == "active").ToList();
-- var orders = localDbContext.Orders.Where(o => o.OrderDate > cutoffDate).ToList();
-- var result = users.Join(orders, u => u.UserId, o => o.UserId, ...);

-- Option 4: Materialized view / summary table
-- Create summary table on remote server, replicate to local
CREATE TABLE dbo.UserSummary (
    user_id INT PRIMARY KEY,
    username NVARCHAR(100),
    status NVARCHAR(20),
    last_login DATETIME
);
-- Populate via scheduled job from remote server

-- Option 5: Web API / REST service
-- Application calls remote API, gets JSON, joins locally
-- This decouples database schemas and improves security

-- Option 6: If linked server is unavoidable, use OPENQUERY for predicate pushdown
SELECT *
FROM OPENQUERY(
    [RemoteServer],
    'SELECT user_id, username, status
     FROM RemoteDB.dbo.Users
     WHERE status = ''active'' AND last_login > ''2024-01-01'''
);
-- Query is executed entirely on remote server (faster)
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
    { "id": "linked-server", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
