# Avoid Select Star

**Rule ID:** `avoid-select-star`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Avoid SELECT * in queries.

## Rationale

`SELECT *` causes performance issues, schema brittleness, and maintenance problems.

**Performance issues**:

1. **Unnecessary data transfer**: Retrieves all columns even if only a few are needed
   - Network overhead: Transferring megabytes of BLOB/TEXT data unnecessarily
   - Memory waste: Loading unused columns into application memory
   - I/O overhead: Reading data pages that aren't needed

2. **Prevents index optimizations**: Query optimizer cannot use covering indexes
   ```sql
   -- Index on (CustomerId, OrderDate) cannot be used as covering index
   SELECT * FROM Orders WHERE CustomerId = 100;  -- Requires table lookup

   -- Covering index can be used (no table lookup)
   SELECT OrderId, OrderDate FROM Orders WHERE CustomerId = 100;
   ```

3. **Slower execution plans**: Forces table scans or index scans with lookups
   - Covering index: All required columns are in the index (fast)
   - Non-covering: Must look up additional columns from table (slow)

**Schema brittleness**:

1. **Column order dependency**: Adding columns breaks INSERT...SELECT
   ```sql
   -- Table initially: (Id, Name)
   CREATE TABLE Users (Id INT, Name VARCHAR(50));

   -- Works initially
   INSERT INTO Users SELECT * FROM TempUsers;

   -- Later, Email column added at position 2: (Id, Email, Name)
   ALTER TABLE Users ADD Email VARCHAR(100);

   -- Now INSERT fails: column count mismatch or wrong data in wrong columns!
   INSERT INTO Users SELECT * FROM TempUsers;  -- Error or data corruption
   ```

2. **New columns break code**: Adding columns causes unexpected behavior
   - API responses include extra fields (breaks client schema validation)
   - CSV exports have extra columns (breaks downstream processing)
   - Report layouts break (extra columns exceed page width)

3. **Security exposure**: New sensitive columns automatically exposed
   ```sql
   SELECT * FROM Users;  -- Initially returns (Id, Name)
   -- Later, SSN column added
   -- Now exposes SSN to all existing queries (security breach!)
   ```

**Maintenance issues**:

1. **Unclear intent**: Impossible to know which columns are actually used
   ```sql
   SELECT * FROM Orders;  -- Which columns does the application need?
   ```

2. **Dead column detection**: Cannot identify unused columns for removal
   - With explicit columns, you can search codebase for usage
   - With `*`, you must assume all columns are used

3. **Code review difficulty**: Reviewers can't verify correct columns are selected

**When SELECT * is acceptable**:

1. **Ad-hoc queries**: Interactive exploration in SSMS (not production code)
2. **EXISTS checks**: `WHERE EXISTS (SELECT * FROM ...)` (only checks existence, no data returned)
3. **COUNT**: `SELECT COUNT(*) FROM ...` (no columns returned, just count)
4. **Temporary tables**: `SELECT * INTO #Temp FROM ...` when all columns are needed
5. **Table variables**: Small scope, short-lived, all columns needed

**Best practice**: Always list explicit columns in production code.

## Examples

### Bad

```sql
-- SELECT * in production query (bad performance, unclear intent)
SELECT * FROM Users;

-- SELECT * prevents covering index usage
SELECT * FROM Orders WHERE CustomerId = 100;

-- SELECT * in API endpoint (exposes all columns, including future ones)
CREATE PROCEDURE uspGetUserById @UserId INT
AS
BEGIN
    SELECT * FROM Users WHERE UserId = @UserId;  -- Exposes all columns
END;

-- SELECT * in JOIN (retrieves unnecessary columns from both tables)
SELECT * FROM Orders o
JOIN Customers c ON o.CustomerId = c.CustomerId;

-- SELECT * with large BLOB columns (huge network/memory overhead)
SELECT * FROM Documents;  -- Includes multi-megabyte DocumentContent column

-- SELECT * in INSERT...SELECT (breaks when table schema changes)
INSERT INTO ArchivedUsers
SELECT * FROM Users WHERE LastLoginDate < '2020-01-01';

-- SELECT * in view (exposes all columns, including sensitive data)
CREATE VIEW vwActiveUsers AS
SELECT * FROM Users WHERE Active = 1;

-- SELECT * in subquery (retrieves unnecessary data)
SELECT o.OrderId, (SELECT * FROM Customers WHERE CustomerId = o.CustomerId) AS CustomerInfo
FROM Orders o;
```

### Good

```sql
-- Explicit column list (clear intent, good performance)
SELECT UserId, UserName, Email FROM Users;

-- Explicit columns allow covering index
SELECT OrderId, OrderDate, Total FROM Orders WHERE CustomerId = 100;

-- API endpoint with explicit columns (controlled schema)
CREATE PROCEDURE uspGetUserById @UserId INT
AS
BEGIN
    SELECT UserId, UserName, Email, CreatedDate
    FROM Users
    WHERE UserId = @UserId;
END;

-- Explicit columns in JOIN (only needed columns)
SELECT o.OrderId, o.Total, c.CustomerName, c.Email
FROM Orders o
JOIN Customers c ON o.CustomerId = c.CustomerId;

-- Explicit columns avoid BLOB overhead
SELECT DocumentId, FileName, CreatedDate, FileSize
FROM Documents;  -- Does NOT include DocumentContent

-- INSERT with explicit column list (robust against schema changes)
INSERT INTO ArchivedUsers (UserId, UserName, Email, LastLoginDate)
SELECT UserId, UserName, Email, LastLoginDate
FROM Users
WHERE LastLoginDate < '2020-01-01';

-- View with explicit columns (controlled exposure)
CREATE VIEW vwActiveUsers AS
SELECT UserId, UserName, Email
FROM Users
WHERE Active = 1;

-- Explicit columns in scalar subquery
SELECT o.OrderId,
       (SELECT CustomerName FROM Customers WHERE CustomerId = o.CustomerId) AS CustomerName
FROM Orders o;

-- Acceptable: EXISTS (only checks existence, no data returned)
SELECT o.OrderId FROM Orders o
WHERE EXISTS (SELECT * FROM OrderDetails WHERE OrderId = o.OrderId);

-- Acceptable: COUNT (no columns returned)
SELECT COUNT(*) FROM Users;

-- Acceptable: SELECT INTO temporary table when all columns needed
SELECT * INTO #TempOrders FROM Orders WHERE OrderDate > '2024-01-01';
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
    { "id": "avoid-select-star", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
