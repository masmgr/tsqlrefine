# Set Nocount

**Rule ID:** `set-nocount`
**Category:** Configuration
**Severity:** Warning
**Fixable:** No

## Description

Files should start with SET NOCOUNT ON within the first 10 statements to suppress row count messages.

## Rationale

SET NOCOUNT ON prevents SQL Server from sending row count messages ("n rows affected") to the client after each DML statement.

**Benefits**:
1. **Reduced network traffic**: Eliminates row count messages for each INSERT/UPDATE/DELETE
2. **Slight performance improvement**: Less overhead in stored procedures with many statements
3. **Cleaner output**: Application logs and traces are less cluttered
4. **Best practice**: Microsoft recommendation for stored procedures and scripts

**Impact of omitting**:
- Each INSERT/UPDATE/DELETE sends a separate row count message
- Stored procedures with 50+ statements generate 50+ messages
- Negligible in single queries, noticeable in high-volume procedures and batch scripts

**Where to use**:
- First statement in stored procedures
- First statement in triggers
- Beginning of batch scripts
- Not necessary in ad-hoc queries or simple SELECT statements

## Examples

### Bad

```sql
-- No SET NOCOUNT ON at the beginning
CREATE PROCEDURE uspProcessOrders
AS
BEGIN
    -- Missing SET NOCOUNT ON
    UPDATE Orders SET Status = 'Processing' WHERE Status = 'Pending';
    -- Sends "(15 rows affected)" message

    INSERT INTO OrderLog (Message) VALUES ('Processing started');
    -- Sends "(1 row affected)" message

    SELECT * FROM Orders WHERE Status = 'Processing';
END;
```

### Good

```sql
-- SET NOCOUNT ON as first statement
CREATE PROCEDURE uspProcessOrders
AS
BEGIN
    SET NOCOUNT ON;  -- Suppresses row count messages

    UPDATE Orders SET Status = 'Processing' WHERE Status = 'Pending';
    -- No row count message sent

    INSERT INTO OrderLog (Message) VALUES ('Processing started');
    -- No row count message sent

    SELECT * FROM Orders WHERE Status = 'Processing';
    -- Only result set returned, no extra messages
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
    { "id": "set-nocount", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
