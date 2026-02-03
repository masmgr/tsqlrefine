# Escape Keyword Identifier

**Rule ID:** `escape-keyword-identifier`
**Category:** Correctness
**Severity:** Warning
**Fixable:** Yes

## Description

Warns when a T-SQL soft keyword is used as a table/column identifier without escaping, and offers an autofix to bracket it.

## Rationale

T-SQL has two types of keywords:

1. **Reserved keywords**: Cause parse errors when used as identifiers (e.g., `SELECT`, `FROM`, `WHERE`, `ORDER`)
2. **Soft keywords**: Parse successfully as identifiers but should still be escaped for clarity

This rule detects **soft keywords** - words that SQL Server allows as unquoted identifiers in certain contexts, but can cause confusion or future compatibility issues.

**Why escape soft keywords?**

1. **Readability**: Soft keywords look like SQL syntax, making code harder to understand
   ```sql
   SELECT value FROM data;  -- Is 'value' a column? Is 'data' a table?
   SELECT [value] FROM [data];  -- Clear: both are identifiers
   ```

2. **Forward compatibility**: Microsoft may reserve these keywords in future SQL Server versions

3. **Consistency**: Escaping all keyword-like identifiers makes the codebase more maintainable

**Detected soft keywords**:

This rule checks for the following soft keywords:

| Category | Keywords |
|----------|----------|
| Common names | `VALUE`, `TYPE`, `NAME`, `STATUS`, `DATA`, `ROLE`, `LEVEL`, `STATE` |
| Date/Time | `DATE`, `TIME`, `YEAR`, `MONTH`, `DAY`, `HOUR`, `MINUTE`, `SECOND` |
| Aggregates | `COUNT`, `SUM`, `MIN`, `MAX`, `AVG`, `FIRST`, `LAST` |
| I/O | `INPUT`, `OUTPUT`, `PATH`, `CONTENT`, `DOCUMENT` |
| Other | `LANGUAGE`, `ABSOLUTE`, `RELATIVE`, `ROWCOUNT` |

**Note**: Reserved keywords like `ORDER`, `GROUP`, `TABLE`, `SELECT` cause parse errors and are not detected by this rule - the SQL parser itself will reject them.

**Solution: Bracket escaping**:

```sql
CREATE TABLE Items (ItemId INT, [value] INT, [type] VARCHAR(50));
SELECT [value], [type] FROM Items;
```

**Best practice**:

1. **Avoid soft keywords**: Use descriptive names (ItemValue instead of value)
2. **If unavoidable**: Always escape with brackets `[keyword]`
3. **Consistent escaping**: Escape in all contexts (DDL, DML, queries)

**Note**: `QUOTED_IDENTIFIER ON` allows using double quotes `"value"` instead of brackets, but brackets `[value]` are more portable and standard in T-SQL.

## Examples

### Bad

```sql
-- Soft keyword as table name
SELECT * FROM value;  -- Warning: 'value' is a soft keyword

-- Soft keyword as column name
CREATE TABLE Products (
    ProductId INT,
    value INT,          -- Warning: 'value' is a soft keyword
    type VARCHAR(50)    -- Warning: 'type' is a soft keyword
);

-- Multiple soft keywords
SELECT value, type, status FROM data;  -- Warning: all are soft keywords

-- Soft keyword in qualified column reference
SELECT t.value FROM Items AS t;  -- Warning: 'value' is a soft keyword

-- Soft keywords in CREATE TABLE
CREATE TABLE dbo.test (
    id INT,
    value INT,    -- Warning: 'value' is a soft keyword
    [type] INT    -- OK: already escaped
);
```

### Good

```sql
-- Escaped soft keyword as table name
SELECT * FROM [value];

-- Escaped soft keywords in CREATE TABLE
CREATE TABLE Products (
    ProductId INT,
    [value] INT,
    [type] VARCHAR(50)
);

-- All soft keywords escaped
SELECT [value], [type], [status] FROM [data];

-- Escaped keyword in qualified column reference
SELECT t.[value] FROM Items AS t;

-- Escaped soft keywords in CREATE TABLE
CREATE TABLE dbo.test (
    id INT,
    [value] INT,
    [type] INT
);

-- Best practice: Avoid soft keywords altogether
CREATE TABLE Products (
    ProductId INT,
    ProductValue INT,     -- Better: Descriptive name
    ProductType VARCHAR(50)   -- Better: Descriptive name
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
    { "id": "escape-keyword-identifier", "enabled": false }
  ]
}
```

## Exceptions

The rule does not flag:

- **Reserved keywords**: Words like `ORDER`, `GROUP`, `TABLE`, `SELECT` cause parse errors and are rejected by the SQL parser itself
- **Already escaped identifiers**: Identifiers wrapped in brackets `[value]` or double quotes `"value"`
- **SQL syntax keywords**: Keywords used in their syntactic role (e.g., `INTO` in `INSERT INTO`, `TABLE` in `RETURNS TABLE`)

This rule focuses only on soft keywords that parse successfully but should be escaped for clarity.

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
