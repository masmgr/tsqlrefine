# Conditional Begin End

**Rule ID:** `conditional-begin-end`
**Category:** Style
**Severity:** Information
**Fixable:** Yes

## Description

Require BEGIN/END blocks in conditional statements for clarity and maintainability

## Rationale

BEGIN/END blocks in conditional statements improve maintainability and prevent common bugs.

**Benefits**:

1. **Maintainability**: Easy to add more statements later without introducing bugs
2. **Clarity**: Explicit block boundaries make code structure obvious
3. **Error prevention**: Avoids single-statement assumption bugs

**Common bug without BEGIN/END**:

```sql
IF @x = 1
    SELECT 1;
    SELECT 2;  -- Always executes! Not part of IF block
```

Without BEGIN/END, only the first statement after IF is conditional. Additional statements are always executed, causing logic errors.

## Examples

### Bad

```sql
-- Single-line IF without BEGIN/END (error-prone)
IF @x = 1 SELECT 1;

-- Multi-line IF without BEGIN/END (bug: second statement always executes)
IF @Status = 'Active'
    UPDATE Users SET LastSeen = GETDATE();
    SELECT @@ROWCOUNT;  -- Always executes, not part of IF!

-- ELSE without BEGIN/END
IF @Count > 0
    PRINT 'Has records';
ELSE
    PRINT 'No records';  -- OK for single statement, but inconsistent style
```

### Good

```sql
-- IF with explicit BEGIN/END (clear and safe)
IF @x = 1
BEGIN
    SELECT 1;
END;

-- Multi-statement IF with BEGIN/END (correct)
IF @Status = 'Active'
BEGIN
    UPDATE Users SET LastSeen = GETDATE();
    SELECT @@ROWCOUNT;  -- Both statements in IF block
END;

-- Consistent BEGIN/END for all branches
IF @Count > 0
BEGIN
    PRINT 'Has records';
END
ELSE
BEGIN
    PRINT 'No records';
END;

-- Complex conditional with BEGIN/END
IF @OrderTotal > 1000
BEGIN
    UPDATE Orders SET DiscountPercent = 10 WHERE OrderId = @OrderId;
    INSERT INTO OrderLog (OrderId, Message) VALUES (@OrderId, 'Applied bulk discount');
    SELECT @OrderTotal = @OrderTotal * 0.9;  -- Apply 10% discount
END;
```

## Auto-Fix

This rule supports auto-fixing. The fix wraps the single statement with a BEGIN/END block, using proper indentation based on the parent IF or ELSE keyword.

**Before:**

```sql
IF @x = 1
    SELECT 1;
```

**After:**

```sql
IF @x = 1
BEGIN
    SELECT 1;
END
```

The fix preserves the original indentation style and line endings from your source file.

To apply the fix:

```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- fix file.sql
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
    { "id": "conditional-begin-end", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
