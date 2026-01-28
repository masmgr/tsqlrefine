# Ban Query Hints

**Rule ID:** `ban-query-hints`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Detects query hints and table hints that bypass the optimizer, causing long-term maintenance issues.

## Rationale

Query and table hints are "point-in-time fixes" that become technical debt:

- **Statistics change** - Data distributions evolve, hints become obsolete
- **Indexes change** - New indexes added, old ones dropped, hints point to wrong indexes
- **Hardware changes** - More CPUs/memory available, MAXDOP hints limit parallelism
- **SQL Server upgrades** - Optimizer improvements ignored
- **Hard to track** - Scattered across codebase, forgotten over time

What was a performance fix yesterday often becomes a performance **problem** tomorrow, but the hint persists in code.

## Examples

### Bad - Table Hints

```sql
-- Forcing index choice (may become obsolete)
SELECT *
FROM Orders WITH (INDEX(IX_OrderDate))
WHERE CustomerId = @Id;

-- Forcing seek (may not be optimal for all parameter values)
SELECT *
FROM LargeTable WITH (FORCESEEK)
WHERE Status = @Status;

-- Forcing scan (usually a bad idea)
SELECT *
FROM Products WITH (FORCESCAN)
WHERE CategoryId = @CategoryId;

-- Preventing index expansion (SQL Server 2012+)
SELECT *
FROM IndexedView WITH (NOEXPAND)
WHERE Region = @Region;
```

### Bad - Query Hints (OPTION Clause)

```sql
-- Forcing join order (optimizer often knows better)
SELECT *
FROM Orders o
JOIN Customers c ON o.CustomerId = c.Id
OPTION (FORCE ORDER);

-- Fixing parameter sniffing with recompile (expensive)
SELECT *
FROM LargeTable
WHERE Status = @Status
OPTION (RECOMPILE);

-- Limiting parallelism (may hurt on newer hardware)
SELECT COUNT(*)
FROM HugeTable
OPTION (MAXDOP 1);

-- Forcing hash join (optimizer's choice is usually better)
SELECT *
FROM Table1 t1
JOIN Table2 t2 ON t1.Id = t2.Id
OPTION (HASH JOIN);
```

### Good

```sql
-- Let optimizer choose based on current statistics
SELECT *
FROM Orders
WHERE CustomerId = @Id;

-- For parameter sniffing, use better solutions:
-- 1. OPTION (OPTIMIZE FOR UNKNOWN) for stable plans
SELECT *
FROM LargeTable
WHERE Status = @Status
OPTION (OPTIMIZE FOR UNKNOWN);

-- 2. Local variable assignment
DECLARE @LocalStatus VARCHAR(50) = @Status;
SELECT *
FROM LargeTable
WHERE Status = @LocalStatus;

-- 3. Multiple procedures for different scenarios
IF @Status = 'Active'
    EXEC dbo.GetActiveRecords;
ELSE
    EXEC dbo.GetOtherRecords @Status;
```

## Common Scenarios

### Scenario 1: Index Hints from Query Tuning

**Problem:** During tuning, you find forcing an index improves performance.

**Bad Approach:**
```sql
SELECT *
FROM Orders WITH (INDEX(IX_CustomerId_OrderDate))
WHERE CustomerId = @Id AND OrderDate > @StartDate;
```

**Better Approach:**
1. Create missing index if needed
2. Update statistics
3. Let optimizer choose
4. If still slow, investigate query/index design

### Scenario 2: Parameter Sniffing

**Problem:** Different parameter values need different plans.

**Bad Approach:**
```sql
SELECT * FROM LargeTable
WHERE Status = @Status
OPTION (RECOMPILE);  -- Recompile every time (expensive)
```

**Better Approaches:**
```sql
-- Approach 1: OPTIMIZE FOR UNKNOWN (stable plan for all values)
SELECT * FROM LargeTable
WHERE Status = @Status
OPTION (OPTIMIZE FOR UNKNOWN);

-- Approach 2: Split logic for different cardinalities
IF @Status IN ('Active', 'Pending')  -- High cardinality
    SELECT * FROM LargeTable WHERE Status = @Status;
ELSE  -- Low cardinality
    SELECT * FROM LargeTable_Archived WHERE Status = @Status;
```

### Scenario 3: MAXDOP for Specific Queries

**Problem:** Query causes CXPACKET waits with parallelism.

**Bad Approach:**
```sql
SELECT COUNT(*) FROM HugeTable
OPTION (MAXDOP 1);  -- Hard-coded forever
```

**Better Approach:**
- Set server-level MAXDOP appropriately
- Use Resource Governor for workload-specific settings
- Only use query-level MAXDOP as last resort, document why

## When Hints Are Acceptable

Very rarely, hints are necessary:

1. **Documented workaround** for optimizer bug with specific SQL Server version
2. **Temporary fix** during incident (with tracking to remove later)
3. **Very specific scenario** where optimizer consistently makes wrong choice

**IMPORTANT:** Always document:
- Why the hint was added
- SQL Server version
- Expected removal date or condition

```sql
-- WORKAROUND: SQL Server 2019 CU5 optimizer bug #12345
-- TODO: Remove after upgrading to CU8 or later
SELECT *
FROM ProblematicView WITH (NOEXPAND)
WHERE Date = @Date;
```

## Configuration

To disable this rule:

```json
{
  "ruleset": "custom-ruleset.json"
}
```

In `custom-ruleset.json`:

```json
{
  "rules": [
    { "id": "ban-query-hints", "enabled": false }
  ]
}
```

## Coordination with Other Rules

- **NOLOCK hint** is handled by [avoid-nolock](../correctness/avoid-nolock.md) rule
- This rule flags all other table and query hints

## Limitations

- **Cannot determine** if hint is actually beneficial
- **Cannot detect** hints in dynamic SQL
- Requires developer judgment on case-by-case basis

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [avoid-nolock](../correctness/avoid-nolock.md) - Related rule for NOLOCK hint
- [Microsoft Documentation: Query Hints](https://docs.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-query)
- [Microsoft Documentation: Table Hints](https://docs.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table)
