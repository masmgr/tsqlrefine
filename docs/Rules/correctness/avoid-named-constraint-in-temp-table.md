# Avoid Named Constraint In Temp Table

**Rule ID:** `avoid-named-constraint-in-temp-table`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Prohibits named constraints in temporary tables (#TempTable) to avoid naming conflicts in tempdb.

## Rationale

Named constraints in temporary tables cause runtime errors due to tempdb's global constraint namespace.

**Why constraint names conflict**:

1. **Constraint names are global in tempdb**: While `#TempUsers` is session-specific, constraint names like `PK_TempUsers` are **shared across all sessions** in tempdb
2. **Concurrent execution fails**: If two sessions run the same script simultaneously, the second one fails with "constraint name already exists"
3. **Stored procedure re-execution**: Calling a procedure twice in the same session can fail if it drops and recreates temp tables with named constraints
4. **Unpredictable failures**: Timing-dependent failures that are hard to reproduce and debug

**Error message**:

```
Msg 2714, Level 16, State 6
There is already an object named 'PK_TempUsers' in the database.
```

**Why this happens**:

```
Session 1                          Session 2
---------                          ---------
CREATE TABLE #TempUsers (...);
  (creates PK_TempUsers in tempdb)
                                   CREATE TABLE #TempUsers (...);
                                   (tries to create PK_TempUsers)
                                   ERROR: Already exists!
```

**Solutions**:

1. **Use unnamed constraints** (recommended for temp tables):
   ```sql
   CREATE TABLE #TempUsers (
       Id INT PRIMARY KEY  -- SQL Server auto-generates unique name
   );
   ```

2. **Use table variables** (different scope, no tempdb conflicts):
   ```sql
   DECLARE @TempUsers TABLE (
       Id INT PRIMARY KEY,
       Name VARCHAR(50)
   );
   ```

3. **Unique constraint names with @@SPID** (complex, avoid if possible):
   ```sql
   DECLARE @ConstraintName NVARCHAR(128) = 'PK_TempUsers_' + CAST(@@SPID AS NVARCHAR(10));
   EXEC('CREATE TABLE #TempUsers (Id INT CONSTRAINT ' + @ConstraintName + ' PRIMARY KEY)');
   ```

**When is this rule too strict?**

- Single-use temp tables in ad-hoc scripts (controlled execution)
- Dev/test environments with no concurrent execution
- Global temp tables (##GlobalTemp) where naming conflicts are intentional

## Examples

### Bad

```sql
-- Named constraint in temp table (will fail with concurrent execution)
CREATE TABLE #TempUsers (
    Id INT CONSTRAINT PK_TempUsers PRIMARY KEY,
    Name VARCHAR(50) CONSTRAINT UQ_TempUsers_Name UNIQUE
);

-- Stored procedure with named constraint (fails on second call)
CREATE PROCEDURE uspProcessData
AS
BEGIN
    CREATE TABLE #Results (
        ResultId INT CONSTRAINT PK_Results PRIMARY KEY,
        Value DECIMAL(10,2)
    );

    -- Process data...

    DROP TABLE #Results;
END;
-- Calling this procedure twice in the same session may fail!

-- Named foreign key constraint
CREATE TABLE #Orders (
    OrderId INT PRIMARY KEY,
    UserId INT CONSTRAINT FK_Orders_Users FOREIGN KEY REFERENCES Users(UserId)
);
```

### Good

```sql
-- Unnamed constraint (SQL Server generates unique name automatically)
CREATE TABLE #TempUsers (
    Id INT PRIMARY KEY,  -- Auto-generated name like PK__#TempUse__3214EC071A2F3B45
    Name VARCHAR(50) UNIQUE
);

-- Table variable (no tempdb conflicts)
DECLARE @Results TABLE (
    ResultId INT PRIMARY KEY,
    Value DECIMAL(10,2)
);

-- Stored procedure with unnamed constraints (safe for concurrent execution)
CREATE PROCEDURE uspProcessData
AS
BEGIN
    CREATE TABLE #Results (
        ResultId INT PRIMARY KEY,  -- Unique name per session
        Value DECIMAL(10,2)
    );

    -- Process data...

    DROP TABLE #Results;
END;

-- Unnamed foreign key constraint
CREATE TABLE #Orders (
    OrderId INT PRIMARY KEY,
    UserId INT FOREIGN KEY REFERENCES Users(UserId)  -- Auto-generated name
);
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
    { "id": "avoid-named-constraint-in-temp-table", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
