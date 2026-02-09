# Semantic Insert Column Count Mismatch

**Rule ID:** `semantic/insert-column-count-mismatch`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Detects column count mismatches between the target column list and the source in INSERT statements.

## Rationale

Column count mismatch between INSERT target and source causes immediate compile-time errors.

**Compile-time error**:
```
Msg 213, Level 16, State 1
Column name or number of supplied values does not match table definition.
```

**Why this fails**:

1. **Column count must match**: The number of columns in the INSERT target list must exactly match the number of values/columns being inserted
2. **No implicit skipping**: SQL Server will not skip extra columns or use NULL for missing ones
3. **Compile-time validation**: Error is caught before execution, prevents deployment

**Common scenarios**:

1. **Copy-paste errors**: Copied INSERT statement with wrong number of columns
   ```sql
   INSERT INTO Users (Id, Name)        -- 2 columns
   SELECT UserId, UserName, Email      -- 3 columns (mismatch!)
   FROM TempUsers;
   ```

2. **Table schema changes**: Table was modified but INSERT statements not updated
   ```sql
   -- Table initially had 3 columns: (Id, Name, Email)
   -- Later, Email column was removed, but INSERT not updated
   INSERT INTO Users (Id, Name, Email)  -- Email no longer exists
   SELECT 1, 'Alice', 'alice@example.com';
   ```

3. **Wrong SELECT columns**: Selected too many or too few columns
   ```sql
   INSERT INTO Orders (OrderId, CustomerId, Total)  -- 3 columns
   SELECT o.OrderId, o.CustomerId                   -- 2 columns (missing Total)
   FROM TempOrders o;
   ```

4. **VALUES mismatch**: Incorrect number of values in VALUES clause
   ```sql
   INSERT INTO Products (ProductId, Name, Price)    -- 3 columns
   VALUES (1, 'Widget');                            -- 2 values (missing Price)
   ```

**Both INSERT...SELECT and INSERT...VALUES affected**:

- **INSERT...SELECT**: Column count in SELECT must match target column list
- **INSERT...VALUES**: Number of values in VALUES must match target column list
- **UNION/UNION ALL**: All SELECT statements must return same number of columns as target

**Error detection**:

- **Compile-time**: Error occurs when query is compiled, before execution
- **Prevents deployment**: Script fails to deploy to production
- **Easy to spot**: Clear error message with column count details

## Examples

### Bad

```sql
-- INSERT...SELECT with too many columns (3 vs 2)
INSERT INTO Users (Id, Name)
SELECT UserId, UserName, Email FROM TempUsers;  -- Error: 3 columns, expected 2

-- INSERT...SELECT with too few columns (1 vs 2)
INSERT INTO Products (ProductId, Name)
SELECT ProductId FROM TempProducts;  -- Error: 1 column, expected 2

-- INSERT...VALUES with too many values
INSERT INTO Orders (OrderId, CustomerId)
VALUES (1, 100, 999.99);  -- Error: 3 values, expected 2

-- INSERT...VALUES with too few values
INSERT INTO Orders (OrderId, CustomerId, Total)
VALUES (1, 100);  -- Error: 2 values, expected 3

-- UNION with mismatched column count
INSERT INTO Users (Id, Name, Email)
SELECT Id, Name FROM ActiveUsers
UNION ALL
SELECT Id, Name, Email, Phone FROM InactiveUsers;  -- Error: 4 columns, expected 3

-- Copy-paste error (wrong table structure)
INSERT INTO Customers (CustomerId, CustomerName, Email)
SELECT OrderId, CustomerName FROM Orders;  -- Error: 2 columns, expected 3
```

### Good

```sql
-- INSERT...SELECT with matching column count
INSERT INTO Users (Id, Name)
SELECT UserId, UserName FROM TempUsers;  -- Correct: 2 columns

-- INSERT...SELECT with all columns matching
INSERT INTO Products (ProductId, Name, Price, CategoryId)
SELECT ProductId, Name, Price, CategoryId FROM TempProducts;

-- INSERT...VALUES with matching value count
INSERT INTO Orders (OrderId, CustomerId, Total)
VALUES (1, 100, 999.99);  -- Correct: 3 values

-- Multiple VALUES rows with correct count
INSERT INTO Orders (OrderId, CustomerId, Total)
VALUES
    (1, 100, 999.99),
    (2, 101, 799.50),
    (3, 102, 1200.00);

-- UNION with matching column counts
INSERT INTO Users (Id, Name, Email)
SELECT Id, Name, Email FROM ActiveUsers
UNION ALL
SELECT Id, Name, Email FROM InactiveUsers;  -- Correct: 3 columns in both

-- Explicit column selection (prevents errors)
INSERT INTO Customers (CustomerId, CustomerName, Email)
SELECT o.CustomerId, c.Name, c.Email
FROM Orders o
JOIN ContactInfo c ON o.CustomerId = c.CustomerId;
```

## Limitations

This rule cannot verify column counts in the following cases (analysis is skipped):

- **SELECT \***: `INSERT INTO t (a, b) SELECT * FROM t2` — Column count of `*` requires schema information which is not available during static analysis.
- **table.\***: `INSERT INTO t (a, b) SELECT t2.* FROM t2` — Same limitation as `SELECT *`.

For UNION/INTERSECT/EXCEPT queries, this rule checks each branch that has a statically countable SELECT list.

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
    { "id": "semantic/insert-column-count-mismatch", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
