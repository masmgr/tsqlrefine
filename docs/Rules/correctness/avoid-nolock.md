# Avoid NOLOCK

**Rule ID:** `avoid-nolock`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Warns against using the `NOLOCK` table hint or `READ UNCOMMITTED` isolation level, both of which can lead to dirty reads and data inconsistency.

## Rationale

The `NOLOCK` hint and `READ UNCOMMITTED` isolation level bypass normal locking mechanisms, leading to serious data integrity issues:

**Data correctness problems:**
- **Dirty reads**: Can read uncommitted data that may be rolled back
- **Non-repeatable reads**: Same query can return different results within a transaction
- **Missing rows**: Can skip rows during page splits
- **Duplicate rows**: Can read the same row twice during page splits
- **Wrong aggregations**: COUNT, SUM, AVG can return incorrect values

**Common misconceptions:**
- "NOLOCK improves performance" - The performance gain is often minimal and not worth the risk
- "It's okay for reporting" - Even read-only reports should show consistent data
- "We don't have concurrent writes" - Page splits can occur during reads, causing issues

**Better alternatives:**
- `READ COMMITTED` (default): Provides consistent reads with minimal overhead
- `SNAPSHOT` isolation: Provides consistent reads without blocking writers
- `READ COMMITTED SNAPSHOT ISOLATION`: Combines benefits of both

## Examples

### Bad

```sql
-- NOLOCK hint - can return inconsistent data
SELECT * FROM users WITH (NOLOCK);

-- Multiple tables with NOLOCK - compounds the problem
SELECT u.name, o.total
FROM users u WITH (NOLOCK)
INNER JOIN orders o WITH (NOLOCK) ON u.id = o.user_id;

-- READ UNCOMMITTED isolation - same issues as NOLOCK
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SELECT * FROM users;

-- NOLOCK with other hints - still problematic
SELECT * FROM users WITH (NOLOCK, INDEX(idx_name));

-- NOLOCK in subquery - data integrity issues cascade
SELECT u.name
FROM users u
WHERE u.id IN (SELECT user_id FROM orders WITH (NOLOCK));

-- NOLOCK in CTE - affects entire query
WITH UserOrders AS (
    SELECT user_id, COUNT(*) as order_count
    FROM orders WITH (NOLOCK)
    GROUP BY user_id
)
SELECT * FROM UserOrders;
```

### Good

```sql
-- No hint - uses default READ COMMITTED isolation
SELECT * FROM users;

-- Explicit READ COMMITTED (default)
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
SELECT * FROM users;

-- SNAPSHOT isolation - provides consistent reads without blocking
SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
SELECT * FROM users;

-- READPAST - skip locked rows instead of dirty reads
SELECT * FROM users WITH (READPAST);

-- Enable READ COMMITTED SNAPSHOT at database level (recommended)
ALTER DATABASE MyDatabase
SET READ_COMMITTED_SNAPSHOT ON;
-- Now all queries use snapshot reads by default

-- Proper locking hints for specific scenarios
SELECT * FROM users WITH (UPDLOCK);  -- For update intentions
SELECT * FROM users WITH (ROWLOCK);  -- Row-level locking
SELECT * FROM users WITH (READUNCOMMITTEDLOCK); -- If you really need it (rare)
```

## Understanding the Alternatives

### SNAPSHOT Isolation (Recommended for Reports)
```sql
-- Enable at database level
ALTER DATABASE MyDatabase SET ALLOW_SNAPSHOT_ISOLATION ON;

-- Use in queries
SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
BEGIN TRANSACTION;
    SELECT * FROM users;
    -- Data remains consistent even if other transactions modify it
COMMIT TRANSACTION;
```

### READ COMMITTED SNAPSHOT (Recommended Default)
```sql
-- Enable at database level (one-time setup)
ALTER DATABASE MyDatabase SET READ_COMMITTED_SNAPSHOT ON;

-- Now default behavior prevents dirty reads without blocking
SELECT * FROM users;  -- Automatically uses row versioning
```

### When to Use READPAST
```sql
-- Processing queue: skip locked rows instead of dirty reads
SELECT TOP 100 *
FROM work_queue WITH (READPAST)
WHERE status = 'pending'
ORDER BY created_date;
```

## Performance Considerations

- **SNAPSHOT isolation**: Uses tempdb for row versioning; ensure tempdb is properly sized
- **READ COMMITTED SNAPSHOT**: Minimal overhead; recommended for most applications
- **Proper indexing**: Often provides more performance benefit than NOLOCK

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "avoid-nolock", "enabled": false }
  ]
}
```

## Important Notes

- Even for "read-only reports", NOLOCK can return incorrect results
- The performance benefit of NOLOCK is often negligible with proper indexing
- SNAPSHOT isolation usually provides better performance than NOLOCK without the data integrity issues

## See Also

- Transaction isolation level best practices
- Database snapshot isolation configuration
