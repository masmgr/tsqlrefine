# Escape Keyword Identifier

**Rule ID:** `escape-keyword-identifier`
**Category:** Correctness
**Severity:** Warning
**Fixable:** Yes

## Description

Warns when a Transact-SQL keyword is used as a table/column identifier without escaping, and offers an autofix to bracket it.

## Rationale

Using T-SQL reserved keywords as identifiers without escaping causes compile-time errors and forward-compatibility issues.

**Compile-time errors**:

```
Msg 156, Level 15, State 1
Incorrect syntax near the keyword 'order'.
```

**Why this fails**:

1. **Reserved keywords**: SQL Server has 200+ reserved keywords (SELECT, FROM, WHERE, ORDER, GROUP, TABLE, etc.)
   - These keywords have special meaning in T-SQL syntax
   - Cannot be used as table/column/variable names without escaping

2. **Ambiguous syntax**: Parser cannot distinguish between keyword and identifier
   ```sql
   SELECT * FROM order;  -- Error: Is 'order' a table name or ORDER BY clause?
   ```

3. **Context-dependent**: Some keywords work in certain contexts but fail in others
   ```sql
   CREATE TABLE [table] (id INT);  -- OK with brackets
   CREATE TABLE table (id INT);    -- Error: 'table' is reserved keyword
   ```

**Common reserved keywords used as identifiers**:

| Keyword | Common Use Case | Error Without Escaping |
|---------|----------------|------------------------|
| ORDER | Orders table | Syntax error (conflicts with ORDER BY) |
| GROUP | User groups table | Syntax error (conflicts with GROUP BY) |
| TABLE | Metadata table | Syntax error (reserved word) |
| USER | Users table | Syntax error (reserved word) |
| DATE | Date column | Syntax error (reserved word) |
| KEY | Key column | Syntax error (reserved word) |
| LEVEL | Level column | Syntax error (reserved word) |
| OPTION | Options table | Syntax error (conflicts with OPTION clause) |
| INDEX | Index column | Syntax error (reserved word) |
| VIEW | View metadata | Syntax error (reserved word) |

**Forward compatibility issues**:

1. **New reserved keywords**: Each SQL Server version adds new reserved keywords
   - Code that works in SQL Server 2012 may fail in SQL Server 2019
   - Example: `JSON`, `STRING_AGG` became reserved in later versions

2. **Breaking changes**: Upgrading SQL Server can break existing queries
   ```sql
   -- Worked in SQL Server 2012
   CREATE TABLE json (id INT);

   -- Fails in SQL Server 2016+ ('json' is now reserved)
   CREATE TABLE json (id INT);  -- Error!
   ```

**Readability issues**:

Using keywords as identifiers makes code confusing:
```sql
SELECT order FROM order WHERE order > 100;  -- Which 'order' is which?
SELECT [order] FROM [order] WHERE [order] > 100;  -- Clear: all are identifiers
```

**Solution: Bracket escaping**:

```sql
CREATE TABLE [Order] (OrderId INT, [Date] DATE, [User] NVARCHAR(50));
SELECT [Order], [Date], [User] FROM [Order];
```

**Best practice**:

1. **Avoid reserved keywords**: Use non-reserved names (Orders instead of Order)
2. **If unavoidable**: Always escape with brackets `[keyword]`
3. **Consistent escaping**: Escape in all contexts (DDL, DML, queries)

**Note**: `QUOTED_IDENTIFIER ON` allows using double quotes `"order"` instead of brackets, but brackets `[order]` are more portable and standard in T-SQL.

## Examples

### Bad

```sql
-- Reserved keyword as table name (syntax error)
SELECT * FROM order;  -- Error: 'order' is reserved keyword

-- Reserved keyword as column name
CREATE TABLE Products (
    ProductId INT,
    name VARCHAR(100),    -- OK: 'name' is not reserved in this context
    key VARCHAR(50),      -- Error: 'key' is reserved keyword
    index INT             -- Error: 'index' is reserved keyword
);

-- Multiple reserved keywords
SELECT user, group, level FROM table;  -- Error: all are reserved keywords

-- Reserved keyword in JOIN
SELECT o.OrderId, g.GroupName
FROM order o  -- Error: 'order' is reserved keyword
JOIN group g ON o.GroupId = g.GroupId;  -- Error: 'group' is reserved keyword

-- Reserved keyword in WHERE clause
SELECT * FROM Customers WHERE user = 'John';  -- Error: 'user' is reserved keyword

-- Reserved keyword in INSERT
INSERT INTO table (key, value) VALUES (1, 'test');  -- Error: 'table', 'key' are reserved

-- Reserved keyword as alias (error in some contexts)
SELECT OrderId AS order FROM Orders;  -- Error: 'order' is reserved keyword

-- Reserved keyword in stored procedure
CREATE PROCEDURE GetOrderByDate @date DATE  -- Error: 'date' is reserved keyword
AS
BEGIN
    SELECT * FROM Orders WHERE OrderDate = @date;
END;

-- Reserved keyword in variable name
DECLARE @table VARCHAR(50);  -- Error: 'table' is reserved keyword
SET @table = 'Orders';
```

### Good

```sql
-- Escaped reserved keyword as table name
SELECT * FROM [order];  -- OK: Escaped with brackets

-- Escaped reserved keywords in CREATE TABLE
CREATE TABLE Products (
    ProductId INT,
    [name] VARCHAR(100),  -- OK: Can escape even non-reserved words
    [key] VARCHAR(50),    -- OK: Escaped reserved keyword
    [index] INT           -- OK: Escaped reserved keyword
);

-- All reserved keywords escaped
SELECT [user], [group], [level] FROM [table];

-- Escaped keywords in JOIN
SELECT o.OrderId, g.GroupName
FROM [order] o
JOIN [group] g ON o.GroupId = g.GroupId;

-- Escaped keyword in WHERE clause
SELECT * FROM Customers WHERE [user] = 'John';

-- Escaped keywords in INSERT
INSERT INTO [table] ([key], [value]) VALUES (1, 'test');

-- Escaped keyword as alias
SELECT OrderId AS [order] FROM Orders;

-- Escaped keyword in stored procedure parameter
CREATE PROCEDURE GetOrderByDate @date DATE  -- OK: Parameter names don't require escaping
AS
BEGIN
    SELECT * FROM [Order] WHERE OrderDate = @date;
END;

-- Escaped keyword in variable name (still avoid if possible)
DECLARE @table VARCHAR(50);  -- Variables don't require escaping, but avoid keyword names
SET @table = 'Orders';

-- Best practice: Avoid reserved keywords altogether
CREATE TABLE Orders (     -- Better: Plural, not reserved keyword
    OrderId INT,
    ProductKey VARCHAR(50),   -- Better: Descriptive, not just 'key'
    IndexValue INT            -- Better: Descriptive, not just 'index'
);

SELECT UserId, GroupId, LevelValue FROM UserGroups;  -- Better: Non-reserved names
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
    { "id": "escape-keyword-identifier", "enabled": false }
  ]
}
```

## Exceptions

The rule does not flag SQL syntax keywords that are part of compound statement structures:

- `INTO` in `INSERT INTO table_name` or `SELECT ... INTO #temp`
- Other table name context keywords (`FROM`, `JOIN`, `UPDATE`, `MERGE`, `TABLE`) when they appear after another context keyword

These keywords are recognized as part of SQL syntax, not as identifiers.

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
