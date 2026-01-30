# Semantic Data Type Length

**Rule ID:** `semantic/data-type-length`
**Category:** Correctness
**Severity:** Error
**Fixable:** No

## Description

Requires explicit length specification for variable-length data types (VARCHAR, NVARCHAR, CHAR, NCHAR, VARBINARY, BINARY) to prevent silent data truncation.

## Rationale

Omitting length for character types causes **silent data truncation** with default length of 1 byte/character.

**Default lengths when omitted**:

| Type | Default Length | Impact |
|------|----------------|---------|
| VARCHAR | 1 byte | Truncates to first character |
| NVARCHAR | 1 character | Truncates to first character |
| CHAR | 1 byte | Pads/truncates to 1 character |
| NCHAR | 1 character | Pads/truncates to 1 character |
| VARBINARY | 1 byte | Truncates to 1 byte |
| BINARY | 1 byte | Pads/truncates to 1 byte |

**Danger**: **No error or warning** when data is truncated!

**Example of silent data loss**:
```sql
DECLARE @Name VARCHAR;  -- Defaults to VARCHAR(1)
SET @Name = 'Alice';    -- Silently truncates to 'A'
SELECT @Name;           -- Returns 'A', data loss!
```

This silent truncation can cause:
- **Data corruption**: Partial data stored without error
- **Logic errors**: Comparisons fail unexpectedly
- **Debugging nightmares**: No error message to indicate truncation
- **Production incidents**: Data loss discovered only after deployment

**Where this applies**:
- Variable declarations: `DECLARE @x VARCHAR`
- Table columns: `CREATE TABLE t (c VARCHAR)`
- Function parameters: `CREATE FUNCTION f(@Param VARCHAR)`
- Stored procedure parameters: `CREATE PROCEDURE p(@Param VARCHAR)`

## Examples

### Bad

```sql
-- Variable declaration without length (defaults to 1 byte)
DECLARE @Name VARCHAR;  -- Dangerous: VARCHAR(1)
SET @Name = 'Alice';    -- Silently truncates to 'A'

-- Table column without length
CREATE TABLE Users (
    UserId INT PRIMARY KEY,
    UserName VARCHAR,      -- Dangerous: VARCHAR(1)
    Email NVARCHAR         -- Dangerous: NVARCHAR(1)
);

-- Function parameter without length
CREATE FUNCTION dbo.GetUserByName(@Name VARCHAR)  -- Dangerous: VARCHAR(1)
RETURNS TABLE AS RETURN
SELECT * FROM Users WHERE UserName = @Name;  -- Always fails!

-- Multiple declarations
DECLARE @FirstName VARCHAR,   -- VARCHAR(1)
        @LastName VARCHAR,    -- VARCHAR(1)
        @Email NVARCHAR;      -- NVARCHAR(1)
```

### Good

```sql
-- Explicit length specification (safe)
DECLARE @Name VARCHAR(100);  -- Stores full name
SET @Name = 'Alice';         -- Stores 'Alice' completely
SELECT @Name;                -- Returns 'Alice'

-- Table with explicit lengths
CREATE TABLE Users (
    UserId INT PRIMARY KEY,
    UserName VARCHAR(50),    -- Explicit length
    Email NVARCHAR(100),     -- Explicit length for Unicode
    PhoneNumber VARCHAR(20)
);

-- Function with explicit length
CREATE FUNCTION dbo.GetUserByName(@Name VARCHAR(50))  -- Explicit length
RETURNS TABLE AS RETURN
SELECT * FROM Users WHERE UserName = @Name;

-- Modern best practices
DECLARE @Description VARCHAR(MAX);  -- For unpredictable lengths
DECLARE @Email NVARCHAR(100);       -- Unicode support
DECLARE @Code CHAR(10);             -- Fixed-length codes
```

**Modern best practices**:

- **VARCHAR(MAX)**: For unpredictable lengths (use sparingly, impacts performance)
- **VARCHAR(50), VARCHAR(100), VARCHAR(500)**: For typical text fields
- **NVARCHAR(n)**: For Unicode support (emails, names, international text)
- **CHAR(n)**: For fixed-length codes (country codes, status flags)

**Never omit length** unless you explicitly want 1 byte/character (rare).

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
    { "id": "semantic/data-type-length", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
