# Prefer Json Functions

**Rule ID:** `prefer-json-functions`
**Category:** Modernization
**Severity:** Information
**Fixable:** No

## Description

Encourages built-in JSON features (OPENJSON, JSON_VALUE, FOR JSON, etc.) over manual string parsing/building (SQL Server 2016+).

## Rationale

Manual JSON string parsing using `CHARINDEX()`, `SUBSTRING()`, `PATINDEX()`, or string concatenation is **error-prone and inefficient** compared to built-in JSON functions:

1. **Fragile parsing**:
   - String functions break with escaped quotes, nested objects, whitespace variations
   - JSON property order may change, breaking position-based parsing
   - Hard to handle arrays, nested structures, or special characters

2. **Poor performance**:
   - String functions scan character-by-character
   - No optimization for JSON structure
   - Cannot use JSON indexes (available in SQL Server 2016+)

3. **Maintenance nightmare**:
   - Complex CHARINDEX/SUBSTRING chains are hard to understand
   - Easy to introduce bugs when modifying parsing logic
   - No validation of JSON syntax

4. **Built-in JSON functions are better**:
   - **JSON_VALUE()**: Extract scalar values (strings, numbers, booleans)
   - **JSON_QUERY()**: Extract objects or arrays
   - **OPENJSON()**: Parse JSON into relational rows
   - **FOR JSON**: Generate JSON from query results
   - **ISJSON()**: Validate JSON syntax

**Compatibility**: JSON functions available in SQL Server 2016+ (compat level 130+).

## Examples

### Bad

```sql
-- Manual parsing with CHARINDEX (fragile)
SELECT SUBSTRING(json_data,
    CHARINDEX('"name":', json_data) + 8,
    CHARINDEX(',', json_data, CHARINDEX('"name":', json_data)) - CHARINDEX('"name":', json_data) - 8
) AS name
FROM data;

-- Building JSON manually with string concatenation (error-prone)
SELECT '{' +
    '"id":' + CAST(id AS VARCHAR) + ',' +
    '"name":"' + name + '",' +
    '"active":' + CASE WHEN active = 1 THEN 'true' ELSE 'false' END +
'}' AS json_output
FROM users;  -- Breaks if name contains quotes!

-- PATINDEX for JSON extraction (complex)
SELECT SUBSTRING(json_col,
    PATINDEX('%"email"%', json_col),
    PATINDEX('%}%', json_col) - PATINDEX('%"email"%', json_col)
) AS email_fragment
FROM users;

-- Manual JSON array handling (impossible to maintain)
DECLARE @json NVARCHAR(MAX) = '[1,2,3,4,5]';
SELECT SUBSTRING(@json, 2, LEN(@json) - 2);  -- Strip brackets, but how to split?
```

### Good

```sql
-- Extract scalar value with JSON_VALUE
SELECT JSON_VALUE(json_data, '$.name') AS name
FROM data;

-- Extract nested value
SELECT JSON_VALUE(json_data, '$.user.email') AS email
FROM data;

-- Extract array element
SELECT JSON_VALUE(json_data, '$.tags[0]') AS first_tag
FROM data;

-- Generate JSON with FOR JSON
SELECT id, name, active
FROM users
FOR JSON PATH;
-- Result: [{"id":1,"name":"John","active":true},...]

-- Generate JSON with specific structure
SELECT id AS 'user.id',
       name AS 'user.name',
       email AS 'user.email'
FROM users
FOR JSON PATH, ROOT('users');

-- Parse JSON array into rows with OPENJSON
DECLARE @json NVARCHAR(MAX) = '[1,2,3,4,5]';
SELECT value FROM OPENJSON(@json);
-- Result rows: 1, 2, 3, 4, 5

-- Parse JSON object into columns
DECLARE @user NVARCHAR(MAX) = '{"id":1,"name":"John","email":"john@example.com"}';
SELECT *
FROM OPENJSON(@user)
WITH (
    id INT '$.id',
    name NVARCHAR(100) '$.name',
    email NVARCHAR(255) '$.email'
);

-- Validate JSON before parsing
IF ISJSON(@json_input) = 1
BEGIN
    SELECT JSON_VALUE(@json_input, '$.data');
END
ELSE
BEGIN
    RAISERROR('Invalid JSON', 16, 1);
END;

-- Extract object/array with JSON_QUERY
SELECT JSON_QUERY(json_data, '$.address') AS address_object,
       JSON_QUERY(json_data, '$.tags') AS tags_array
FROM data;

-- Modify JSON (SQL Server 2016+)
UPDATE data
SET json_data = JSON_MODIFY(json_data, '$.status', 'active');
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
    { "id": "prefer-json-functions", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
