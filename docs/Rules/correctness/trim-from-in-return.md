# Trim From In Return

**Rule ID:** `trim-from-in-return`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects TRIM with FROM clause inside RETURN statements, which fails to parse due to a known ScriptDOM bug. Workaround: assign the result to a variable first.

## Rationale

`Microsoft.SqlServer.TransactSql.ScriptDom` has a known parsing bug (error 46010) where `TRIM` with a `FROM` clause fails when used directly inside a `RETURN` statement. This affects all `TRIM` variants that use the `FROM` clause:

- `TRIM('x' FROM @str)`
- `TRIM(LEADING 'x' FROM @str)`
- `TRIM(TRAILING 'x' FROM @str)`
- `TRIM(BOTH 'x' FROM @str)`
- `TRIM(NCHAR(12288) FROM @str)`

The SQL itself is **valid T-SQL** that executes correctly on SQL Server — the issue is a ScriptDOM parser limitation, not a T-SQL syntax error.

Because ScriptDOM fails to parse the file, the linter emits a `parse-error` (Error) alongside this `trim-from-in-return` (Warning). This rule identifies that the `parse-error` is caused by the known TRIM bug and guides you toward the workaround.

**Affected ScriptDOM versions**: 160.x and 170.x (at minimum).

## Examples

### Bad

```sql
-- TRIM with FROM clause directly in RETURN causes a parse error
CREATE FUNCTION dbo.TrimChars(@str NVARCHAR(MAX))
RETURNS NVARCHAR(MAX)
AS
BEGIN
    RETURN TRIM('x' FROM @str);
END;

-- Also affects LEADING/TRAILING/BOTH variants
CREATE FUNCTION dbo.TrimLeading(@str NVARCHAR(MAX))
RETURNS NVARCHAR(MAX)
AS
BEGIN
    RETURN TRIM(LEADING ' ' FROM @str);
END;
```

### Good

```sql
-- Workaround: assign to a variable first, then return it
CREATE FUNCTION dbo.TrimChars(@str NVARCHAR(MAX))
RETURNS NVARCHAR(MAX)
AS
BEGIN
    DECLARE @result NVARCHAR(MAX) = TRIM('x' FROM @str);
    RETURN @result;
END;

-- TRIM without FROM clause parses fine in RETURN
CREATE FUNCTION dbo.TrimSpaces(@str NVARCHAR(MAX))
RETURNS NVARCHAR(MAX)
AS
BEGIN
    RETURN TRIM(@str);
END;

-- TRIM with FROM clause parses fine in SELECT and SET
SELECT TRIM('x' FROM col) FROM dbo.MyTable;
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
    { "id": "trim-from-in-return", "enabled": false }
  ]
}
```

To suppress the accompanying `parse-error` diagnostic inline:

```sql
-- tsqlrefine-disable parse-error
CREATE FUNCTION dbo.TrimChars(@str NVARCHAR(MAX))
RETURNS NVARCHAR(MAX)
AS
BEGIN
    RETURN TRIM('x' FROM @str);
END;
-- tsqlrefine-enable parse-error
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Documentation](../../configuration.md)
