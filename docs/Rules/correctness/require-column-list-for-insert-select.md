# Require Column List For Insert Select

**Rule ID:** `require-column-list-for-insert-select`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

INSERT SELECT statements must explicitly specify the column list to avoid errors when table schema changes

## Rationale

INSERT...SELECT without explicit column list breaks when table schema changes, causing runtime errors or silent data corruption.

**Why implicit column mapping fails**:

1. **Column order dependency**: INSERT without column list uses positional mapping by column order
   ```sql
   -- Table initially: (Id INT, Name NVARCHAR(50), Email NVARCHAR(100))
   INSERT INTO Users SELECT * FROM TempUsers;  -- Inserts: Id → Id, Name → Name, Email → Email

   -- Later, Email moved to position 2: (Id INT, Email NVARCHAR(100), Name NVARCHAR(50))
   INSERT INTO Users SELECT * FROM TempUsers;  -- Now inserts: Id → Id, Name → Email, Email → Name (WRONG!)
   ```

2. **New columns cause errors**: Adding columns to target table breaks INSERT
   ```sql
   -- Table initially: Users (Id, Name)
   INSERT INTO Users SELECT * FROM TempUsers;  -- Works: 2 columns → 2 columns

   -- Later, CreatedDate column added: Users (Id, Name, CreatedDate)
   INSERT INTO Users SELECT * FROM TempUsers;  -- Error: 2 columns → 3 columns (mismatch!)
   ```

3. **Removed columns cause errors**: Dropping columns from source table breaks INSERT
   ```sql
   -- Initially: TempUsers (Id, Name, Email)
   INSERT INTO Users SELECT * FROM TempUsers;  -- Works: 3 columns → 3 columns

   -- Later, Email dropped: TempUsers (Id, Name)
   INSERT INTO Users SELECT * FROM TempUsers;  -- Error: 2 columns → 3 columns (mismatch!)
   ```

**Silent data corruption scenarios**:

1. **Wrong data type mapping**: Columns have compatible types but wrong semantics
   ```sql
   -- Target: Users (UserId INT, UserName NVARCHAR(50), UserEmail NVARCHAR(100))
   -- Source: TempUsers (UserId INT, UserEmail NVARCHAR(100), UserName NVARCHAR(50))

   INSERT INTO Users SELECT * FROM TempUsers;
   -- Result: UserName gets email, UserEmail gets name (no error, silent corruption!)
   ```

2. **Column reordering**: Refactoring changes column order, breaks positional mapping
   ```sql
   -- Before: Products (ProductId, ProductName, Price)
   INSERT INTO Products SELECT * FROM TempProducts;  -- Works

   -- After refactoring: Products (ProductId, Price, ProductName)
   INSERT INTO Products SELECT * FROM TempProducts;  -- Inserts ProductName into Price, Price into ProductName!
   ```

**Runtime errors**:

```
Msg 213, Level 16, State 1
Column name or number of supplied values does not match table definition.
```

**When errors occur**:

- Adding columns to target table (column count mismatch)
- Removing columns from source table (column count mismatch)
- Reordering columns in either table (positional mapping breaks)
- Changing column data types (may cause conversion errors)

**Why explicit column list is safe**:

```sql
INSERT INTO Users (UserId, UserName, UserEmail)
SELECT UserId, UserName, UserEmail FROM TempUsers;
```

Benefits:
1. **Column name mapping**: Maps by name, not position (order-independent)
2. **Schema change resilience**: Adding new columns doesn't break INSERT
3. **Clear intent**: Obvious which columns are being inserted
4. **Compile-time validation**: SQL Server validates column names exist
5. **Self-documenting**: Code shows exactly what's being inserted

**Exception: SELECT INTO** (creates new table):

```sql
SELECT * INTO #TempUsers FROM Users;  -- OK: Creating new table with same schema
```

This is safe because it creates the destination table with the exact schema of the source.

## Examples

### Bad

```sql
-- INSERT without column list (breaks on schema changes)
INSERT INTO Users SELECT * FROM TempUsers;

-- INSERT with SELECT * (positional mapping, fragile)
INSERT INTO Orders SELECT * FROM TempOrders;

-- INSERT with multiple columns, no target list
INSERT INTO Products SELECT ProductId, ProductName, Price FROM TempProducts;

-- INSERT from JOIN without column list
INSERT INTO OrderSummary
SELECT o.OrderId, c.CustomerName, o.Total
FROM Orders o
JOIN Customers c ON o.CustomerId = c.CustomerId;

-- INSERT with UNION without column list
INSERT INTO AllUsers
SELECT * FROM ActiveUsers
UNION ALL
SELECT * FROM InactiveUsers;

-- INSERT with subquery without column list
INSERT INTO CustomerStats
SELECT CustomerId, (SELECT COUNT(*) FROM Orders WHERE CustomerId = c.CustomerId)
FROM Customers c;

-- Archive operation without column list (fragile)
INSERT INTO ArchivedOrders SELECT * FROM Orders WHERE OrderDate < '2020-01-01';
```

### Good

```sql
-- INSERT with explicit column list (resilient to schema changes)
INSERT INTO Users (UserId, UserName, UserEmail)
SELECT UserId, UserName, UserEmail FROM TempUsers;

-- INSERT with explicit target columns
INSERT INTO Orders (OrderId, CustomerId, OrderDate, Total)
SELECT OrderId, CustomerId, OrderDate, Total FROM TempOrders;

-- Explicit columns (order-independent mapping)
INSERT INTO Products (ProductId, ProductName, Price)
SELECT ProductId, ProductName, Price FROM TempProducts;

-- JOIN with explicit column list
INSERT INTO OrderSummary (OrderId, CustomerName, OrderTotal)
SELECT o.OrderId, c.CustomerName, o.Total
FROM Orders o
JOIN Customers c ON o.CustomerId = c.CustomerId;

-- UNION with explicit column list
INSERT INTO AllUsers (UserId, UserName, Email, IsActive)
SELECT UserId, UserName, Email, 1 FROM ActiveUsers
UNION ALL
SELECT UserId, UserName, Email, 0 FROM InactiveUsers;

-- Subquery with explicit column list
INSERT INTO CustomerStats (CustomerId, OrderCount)
SELECT CustomerId, (SELECT COUNT(*) FROM Orders WHERE CustomerId = c.CustomerId)
FROM Customers c;

-- Archive with explicit columns (clear and safe)
INSERT INTO ArchivedOrders (OrderId, CustomerId, OrderDate, Total, Status)
SELECT OrderId, CustomerId, OrderDate, Total, Status
FROM Orders
WHERE OrderDate < '2020-01-01';

-- Partial column insert (only specific columns)
INSERT INTO Users (UserId, UserName)  -- Email intentionally omitted (defaults to NULL)
SELECT UserId, UserName FROM TempUsers;

-- INSERT with column reordering (explicit list allows different order)
INSERT INTO Users (UserEmail, UserName, UserId)  -- Different order than source
SELECT Email, Name, Id FROM TempUsers;  -- Maps by position in explicit list

-- SELECT INTO is OK (creates new table, no schema mismatch risk)
SELECT * INTO #TempUserBackup FROM Users;  -- Acceptable exception

-- INSERT with default values for missing columns
INSERT INTO Orders (OrderId, CustomerId, OrderDate)  -- Total defaults to NULL
SELECT OrderId, CustomerId, OrderDate FROM TempOrders;
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
    { "id": "require-column-list-for-insert-select", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
