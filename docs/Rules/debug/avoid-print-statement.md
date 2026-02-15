# Avoid Print Statement

**Rule ID:** `avoid-print-statement`
**Category:** Style
**Severity:** Information
**Fixable:** No

## Description

Prohibit PRINT statements; use RAISERROR for error messages and debugging

## Rationale

`PRINT` statements have **significant limitations** compared to other messaging and debugging approaches:

1. **Messages arrive only at completion**:
   - PRINT buffers messages and sends them only when the batch completes or buffer fills
   - Cannot see incremental progress during long-running operations
   - RAISERROR with NOWAIT sends messages immediately

2. **Limited message length**: PRINT truncates messages at 8,000 characters (or 4,000 for nvarchar)
   - Cannot print large JSON/XML documents
   - Error messages may be cut off

3. **Cannot capture in application code easily**:
   - PRINT goes to the Messages tab in SSMS, not to result sets
   - Application frameworks (Entity Framework, ADO.NET) have limited access to PRINT output
   - Error messages via RAISERROR are easier to capture programmatically

4. **No severity levels**: PRINT doesn't indicate importance (info vs. warning vs. error)
   - RAISERROR supports severity levels (0-25)
   - Cannot filter PRINT output by severity

5. **Production code smell**: PRINT is primarily for debugging
   - Should be removed before production deployment
   - Presence in code suggests incomplete development

**Better alternatives:**
- **RAISERROR ... WITH NOWAIT**: Immediate message output with severity levels
- **Logging tables**: Structured logging with timestamps, levels, context
- **Application logging**: Use application framework logging (Serilog, NLog, etc.)
- **Extended Events / SQL Trace**: For performance debugging
- **SELECT for result sets**: Return data as result sets instead of messages

## Examples

### Bad

```sql
-- PRINT for debugging (buffered, arrives late)
PRINT 'Starting data import...';
-- Long-running operation
INSERT INTO target SELECT * FROM source;
PRINT 'Import complete.';
-- Messages appear only after INSERT completes

-- PRINT for error messages (hard to capture)
IF @ErrorCondition = 1
BEGIN
    PRINT 'Error: Invalid data detected';
END;

-- PRINT in loop (messages buffered)
DECLARE @i INT = 1;
WHILE @i <= 100
BEGIN
    PRINT 'Processing row ' + CAST(@i AS VARCHAR);
    -- Process row
    SET @i = @i + 1;
END;
-- All 100 messages appear at once at the end

-- PRINT for large messages (truncated)
DECLARE @LargeXml XML = (SELECT * FROM users FOR XML PATH);
PRINT CAST(@LargeXml AS NVARCHAR(MAX));
-- Truncated at 4,000 characters!

-- PRINT in stored procedure (can't return to caller easily)
CREATE PROCEDURE dbo.ProcessOrders AS
BEGIN
    PRINT 'Processing started';
    -- Logic here
    PRINT 'Processing complete';
END;
```

### Good

```sql
-- RAISERROR with NOWAIT (immediate output)
RAISERROR('Starting data import...', 0, 1) WITH NOWAIT;
INSERT INTO target SELECT * FROM source;
RAISERROR('Import complete.', 0, 1) WITH NOWAIT;

-- RAISERROR for error messages (with severity)
IF @ErrorCondition = 1
BEGIN
    RAISERROR('Error: Invalid data detected', 16, 1);
END;

-- RAISERROR in loop (immediate feedback)
DECLARE @i INT = 1;
WHILE @i <= 100
BEGIN
    RAISERROR('Processing row %d', 0, 1, @i) WITH NOWAIT;
    -- Process row
    SET @i = @i + 1;
END;

-- Logging table for structured logging
CREATE TABLE dbo.ProcessLog (
    log_id INT IDENTITY PRIMARY KEY,
    log_time DATETIME DEFAULT GETDATE(),
    log_level VARCHAR(10),
    message NVARCHAR(MAX)
);

INSERT INTO dbo.ProcessLog (log_level, message)
VALUES ('INFO', 'Starting data import');

-- Query result sets instead of messages
SELECT 'Starting data import' AS status, GETDATE() AS timestamp;
-- Process data
SELECT 'Import complete' AS status, GETDATE() AS timestamp, @@ROWCOUNT AS rows_inserted;

-- Return status via OUTPUT parameter
CREATE PROCEDURE dbo.ProcessOrders
    @StatusMessage NVARCHAR(255) OUTPUT
AS
BEGIN
    -- Logic here
    SET @StatusMessage = 'Processing complete';
END;

-- Extended Events for performance debugging
CREATE EVENT SESSION debug_session ON SERVER
ADD EVENT sqlserver.sql_statement_completed
WHERE sqlserver.database_name = 'MyDatabase';

-- Application-level logging (pseudo-code)
-- C# with Serilog:
-- Log.Information("Starting data import");
-- dbContext.BulkInsert(data);
-- Log.Information("Import complete, {RowCount} rows inserted", rowCount);

-- THROW for errors (better than RAISERROR for error handling)
IF @ErrorCondition = 1
BEGIN
    THROW 50001, 'Invalid data detected', 1;
END;
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
    { "id": "avoid-print-statement", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
