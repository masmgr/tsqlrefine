# Require Column List For Insert Values

**Rule ID:** `require-column-list-for-insert-values`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

INSERT VALUES statements must explicitly specify the column list to avoid errors when table schema changes

## Rationale

INSERT...VALUES without explicit column list breaks when table schema changes, causing runtime errors or silent data corruption.

**Why implicit column mapping fails**:

1. **Column order dependency**: INSERT without column list uses positional mapping
   ```sql
   -- Table initially: (Id INT, Name NVARCHAR(50), Email NVARCHAR(100))
   INSERT INTO Users VALUES (1, 'John', 'john@example.com');  -- Id=1, Name='John', Email='john@example.com'

   -- Later, Email moved to position 2: (Id INT, Email NVARCHAR(100), Name NVARCHAR(50))
   INSERT INTO Users VALUES (1, 'John', 'john@example.com');  -- Id=1, Email='John', Name='john@example.com' (WRONG!)
   ```

2. **New columns cause errors**: Adding columns to table breaks INSERT
   ```sql
   -- Table initially: Users (Id, Name)
   INSERT INTO Users VALUES (1, 'John');  -- Works: 2 values → 2 columns

   -- Later, CreatedDate column added: Users (Id, Name, CreatedDate)
   INSERT INTO Users VALUES (1, 'John');  -- Error: 2 values → 3 columns (mismatch!)
   ```

3. **New columns with defaults**: Silent behavior change
   ```sql
   -- Table initially: Users (Id, Name)
   INSERT INTO Users VALUES (1, 'John');  -- Works

   -- Later, IsActive column added with DEFAULT 1: Users (Id, Name, IsActive)
   INSERT INTO Users VALUES (1, 'John');  -- Error! Even though IsActive has default
   ```

**Silent data corruption scenarios**:

1. **Column reordering**: Refactoring changes column order
   ```sql
   -- Before: Products (ProductId, ProductName, Price)
   INSERT INTO Products VALUES (1, 'Widget', 19.99);  -- Works

   -- After refactoring: Products (ProductId, Price, ProductName)
   INSERT INTO Products VALUES (1, 'Widget', 19.99);
   -- Result: ProductId=1, Price='Widget' (conversion error or truncation!), ProductName=19.99 (WRONG!)
   ```

2. **Data type compatibility**: Different columns have compatible types
   ```sql
   -- Table: Orders (OrderId INT, CustomerId INT, Total DECIMAL(10,2))
   INSERT INTO Orders VALUES (100, 200, 999.99);  -- Works

   -- After refactoring: Orders (OrderId INT, Total DECIMAL(10,2), CustomerId INT)
   INSERT INTO Orders VALUES (100, 200, 999.99);
   -- Result: OrderId=100, Total=200.00, CustomerId=999 (no error, but WRONG data!)
   ```

**Runtime errors**:

```
Msg 213, Level 16, State 1
Column name or number of supplied values does not match table definition.
```

**When errors occur**:

- Adding columns to table (even with DEFAULT values)
- Removing columns from table
- Reordering columns (may cause silent corruption if types are compatible)
- Changing column data types (may cause conversion errors)

**Why explicit column list is safe**:

```sql
INSERT INTO Users (Id, Name, Email)
VALUES (1, 'John', 'john@example.com');
```

Benefits:
1. **Order-independent**: Column order doesn't matter (maps by name, not position)
2. **Schema change resilience**: Adding new columns with defaults doesn't break INSERT
3. **Clear intent**: Obvious which columns are being set
4. **Compile-time validation**: SQL Server validates column names exist
5. **Self-documenting**: Code shows exactly what's being inserted
6. **Partial inserts**: Can insert subset of columns (others get defaults or NULL)

**Example: Schema resilience**

```sql
-- Explicit column list (resilient)
INSERT INTO Users (Id, Name) VALUES (1, 'John');

-- Add new column with default
ALTER TABLE Users ADD CreatedDate DATETIME DEFAULT GETDATE();

-- Same INSERT still works! CreatedDate gets default value
INSERT INTO Users (Id, Name) VALUES (2, 'Jane');  -- CreatedDate auto-populated
```

## Examples

### Bad

```sql
-- INSERT without column list (breaks on schema changes)
INSERT INTO Users VALUES (1, 'John', 'john@example.com');

-- Multiple VALUES rows without column list
INSERT INTO Products VALUES
    (1, 'Widget', 19.99),
    (2, 'Gadget', 29.99),
    (3, 'Doohickey', 39.99);

-- INSERT with DEFAULT keyword (still needs column list)
INSERT INTO Orders VALUES (1, 100, DEFAULT, 999.99);

-- INSERT in stored procedure without column list (fragile)
CREATE PROCEDURE uspCreateUser
    @Id INT,
    @Name NVARCHAR(50),
    @Email NVARCHAR(100)
AS
BEGIN
    INSERT INTO Users VALUES (@Id, @Name, @Email);  -- Breaks if schema changes
END;

-- INSERT with NULL values (unclear which columns)
INSERT INTO Customers VALUES (1, 'Acme Corp', NULL, NULL);

-- INSERT with function calls (unclear mapping)
INSERT INTO AuditLog VALUES (NEWID(), GETDATE(), 'Login', 'User123');

-- INSERT in migration script (very fragile)
INSERT INTO Settings VALUES (1, 'MaxRetries', '3');
INSERT INTO Settings VALUES (2, 'Timeout', '30');

-- INSERT from application code (hardcoded positional values)
INSERT INTO Orders VALUES (100, 200, '2024-01-15', 999.99);
```

### Good

```sql
-- INSERT with explicit column list (resilient)
INSERT INTO Users (Id, Name, Email)
VALUES (1, 'John', 'john@example.com');

-- Multiple VALUES rows with column list
INSERT INTO Products (ProductId, ProductName, Price)
VALUES
    (1, 'Widget', 19.99),
    (2, 'Gadget', 29.99),
    (3, 'Doohickey', 39.99);

-- INSERT with DEFAULT keyword and column list
INSERT INTO Orders (OrderId, CustomerId, OrderDate, Total)
VALUES (1, 100, DEFAULT, 999.99);  -- OrderDate gets DEFAULT value

-- Stored procedure with explicit columns (safe)
CREATE PROCEDURE uspCreateUser
    @Id INT,
    @Name NVARCHAR(50),
    @Email NVARCHAR(100)
AS
BEGIN
    INSERT INTO Users (Id, Name, Email)
    VALUES (@Id, @Name, @Email);
END;

-- Explicit NULL values (clear which columns are NULL)
INSERT INTO Customers (CustomerId, CompanyName, Phone, Fax)
VALUES (1, 'Acme Corp', NULL, NULL);

-- Function calls with explicit columns (clear intent)
INSERT INTO AuditLog (LogId, LogDate, EventType, UserId)
VALUES (NEWID(), GETDATE(), 'Login', 'User123');

-- Migration script with explicit columns (robust)
INSERT INTO Settings (SettingId, SettingKey, SettingValue)
VALUES (1, 'MaxRetries', '3');

INSERT INTO Settings (SettingId, SettingKey, SettingValue)
VALUES (2, 'Timeout', '30');

-- Application INSERT with explicit columns
INSERT INTO Orders (OrderId, CustomerId, OrderDate, Total)
VALUES (100, 200, '2024-01-15', 999.99);

-- Partial column insert (others get defaults or NULL)
INSERT INTO Users (Id, Name)  -- Email defaults to NULL, CreatedDate to GETDATE()
VALUES (1, 'John');

-- Column order different from table definition (explicit list allows this)
INSERT INTO Products (Price, ProductName, ProductId)  -- Different order than table
VALUES (19.99, 'Widget', 1);

-- INSERT with computed/expression values
INSERT INTO Calculations (Id, Value, SquareValue, CubeValue)
VALUES (1, 5, 5*5, 5*5*5);

-- INSERT with IDENTITY column (explicitly excluding it)
INSERT INTO Customers (CompanyName, ContactName)  -- CustomerId is IDENTITY, auto-generated
VALUES ('Acme Corp', 'John Doe');

-- Multiple rows with different column subsets (flexible)
INSERT INTO Events (EventId, EventType, UserId)
VALUES (1, 'Login', 'User123');

INSERT INTO Events (EventId, EventType, AdminId)  -- Different column
VALUES (2, 'AdminAction', 'Admin456');
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
    { "id": "require-column-list-for-insert-values", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
