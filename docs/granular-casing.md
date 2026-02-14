# Granular SQL Element Casing

## Overview

TsqlRefine now supports independent casing control for different SQL elements:
- **Keywords** (SELECT, FROM, WHERE, etc.)
- **Built-in functions** (COUNT, SUM, GETDATE, etc.)
- **Data types** (INT, VARCHAR, DATETIME, etc.)
- **Schema names** (dbo, sys, staging, etc.)
- **Table names and aliases** (Users, Orders, u, o, etc.)
- **Column names and aliases** (UserId, OrderCount, etc.)
- **Variables** (@userId, @@ROWCOUNT, etc.)
- **System tables** (sys.objects, information_schema.columns, etc.)
- **Stored procedures** (sp_helptext, usp_GetUsers, etc.)
- **User-defined functions** (dbo.fn_GetTotal, etc.)

Each element type can be independently set to:
- `Upper` - Convert to UPPERCASE
- `Lower` - Convert to lowercase
- `Pascal` - Convert to PascalCase (e.g., `user_name` → `UserName`, `select` → `Select`)
- `None` - Preserve original casing

## Recommended Defaults

When granular casing is enabled, the following defaults are applied:

| Element Type | Default | Rationale |
|-------------|---------|-----------|
| Keywords | `Upper` | SQL keywords are traditionally uppercase |
| Functions | `Upper` | Built-in functions follow keyword conventions |
| Data Types | `Lower` | Data types are typically lowercase in modern SQL |
| Schemas | `None` | **Safe default** - CS collation environments may break |
| Tables | `None` | **Safe default** - CS collation environments may break |
| Columns | `None` | **Safe default** - CS collation environments may break |
| Variables | `Lower` | Variables use lowercase convention with @ prefix |
| System Tables | `Lower` | System tables are conventionally lowercase |
| Stored Procedures | `None` | **Safe default** - preserve original naming |
| User-Defined Functions | `None` | **Safe default** - preserve original naming |

> **WARNING**: Case-Sensitive collation environments (e.g., Microsoft Fabric Data Warehouse)
> may break queries if identifier casing is changed. The default for Schema/Table/Column
> is `None` (preserve original) for safety. Only change these if you are certain your
> target environment uses Case-Insensitive collation.

## Usage

### Programmatic API

```csharp
using TsqlRefine.Formatting;

var sql = "select u.userid, count(*) as total from dbo.users u";

var options = new FormattingOptions
{
    KeywordElementCasing = ElementCasing.Upper,      // SELECT, FROM, etc.
    BuiltInFunctionCasing = ElementCasing.Upper,     // COUNT, SUM, etc.
    DataTypeCasing = ElementCasing.Lower,            // int, varchar, etc.
    SchemaCasing = ElementCasing.Lower,              // dbo, sys, etc.
    TableCasing = ElementCasing.Upper,               // USERS, ORDERS, etc.
    ColumnCasing = ElementCasing.Upper,              // USERID, TOTAL, etc.
    VariableCasing = ElementCasing.Lower             // @userid, @@rowcount
};

var formatted = SqlFormatter.Format(sql, options);
// Result: SELECT u.USERID, COUNT(*) AS TOTAL FROM dbo.USERS u
```

### Configuration File

In `tsqlrefine.json`:

```json
{
  "formatting": {
    "keywordCasing": "upper",
    "functionCasing": "upper",
    "dataTypeCasing": "lower",
    "schemaCasing": "lower",
    "tableCasing": "upper",
    "columnCasing": "upper",
    "variableCasing": "lower",
    "systemTableCasing": "lower",
    "storedProcedureCasing": "none",
    "userDefinedFunctionCasing": "none"
  }
}
```

Available casing values: `"none"`, `"upper"`, `"lower"`, `"pascal"`

## Examples

### Example 1: All Uppercase Style

```csharp
var options = new FormattingOptions
{
    KeywordElementCasing = ElementCasing.Upper,
    BuiltInFunctionCasing = ElementCasing.Upper,
    DataTypeCasing = ElementCasing.Upper,
    SchemaCasing = ElementCasing.Upper,
    TableCasing = ElementCasing.Upper,
    ColumnCasing = ElementCasing.Upper,
    VariableCasing = ElementCasing.Upper
};
```

**Input:**
```sql
declare @userid int = 1;
select u.userid, count(*) from dbo.users u where u.active = 1;
```

**Output:**
```sql
DECLARE @USERID INT = 1;
SELECT U.USERID, COUNT(*) FROM DBO.USERS U WHERE U.ACTIVE = 1;
```

### Example 2: All Lowercase Style

```csharp
var options = new FormattingOptions
{
    KeywordElementCasing = ElementCasing.Lower,
    BuiltInFunctionCasing = ElementCasing.Lower,
    DataTypeCasing = ElementCasing.Lower,
    SchemaCasing = ElementCasing.Lower,
    TableCasing = ElementCasing.Lower,
    ColumnCasing = ElementCasing.Lower,
    VariableCasing = ElementCasing.Lower
};
```

**Input:**
```sql
DECLARE @USERID INT = 1;
SELECT U.USERID, COUNT(*) FROM DBO.USERS U WHERE U.ACTIVE = 1;
```

**Output:**
```sql
declare @userid int = 1;
select u.userid, count(*) from dbo.users u where u.active = 1;
```

### Example 3: Safe Default Style (Default)

With the default settings, keywords and functions are uppercased, but identifiers (schema/table/column) are preserved:

```csharp
var options = new FormattingOptions
{
    KeywordElementCasing = ElementCasing.Upper,
    BuiltInFunctionCasing = ElementCasing.Upper,
    DataTypeCasing = ElementCasing.Lower,
    SchemaCasing = ElementCasing.None,     // Preserve original
    TableCasing = ElementCasing.None,      // Preserve original
    ColumnCasing = ElementCasing.None,     // Preserve original
    VariableCasing = ElementCasing.Lower
};
```

**Input:**
```sql
DECLARE @UserId INT = 1;
DECLARE @UserName NVARCHAR(50);

SELECT
    U.UserId,
    U.UserName,
    COUNT(*) AS OrderCount,
    SUM(O.TotalAmount) AS TotalSpent,
    GETDATE() AS CurrentDate,
    @@ROWCOUNT AS RowCount
FROM DBO.Users U
INNER JOIN SYS.Orders O ON U.UserId = O.UserId
WHERE U.IsActive = 1
    AND O.OrderDate >= DATEADD(DAY, -30, GETDATE())
GROUP BY U.UserId, U.UserName;
```

**Output:**
```sql
DECLARE @userid int = 1;
DECLARE @username nvarchar(50);

SELECT
    U.UserId,
    U.UserName,
    COUNT(*) AS OrderCount,
    SUM(O.TotalAmount) AS TotalSpent,
    GETDATE() AS CurrentDate,
    @@rowcount AS RowCount
FROM DBO.Users U
INNER JOIN SYS.Orders O ON U.UserId = O.UserId
WHERE U.IsActive = 1
    AND O.OrderDate >= DATEADD(day, -30, GETDATE())
GROUP BY U.UserId, U.UserName;
```

Note: With the default settings, identifiers preserve their original casing (only keywords, functions, data types, and variables are transformed).

### Example 4: Preserve Original Style

```csharp
var options = new FormattingOptions
{
    KeywordElementCasing = ElementCasing.None,
    BuiltInFunctionCasing = ElementCasing.None,
    DataTypeCasing = ElementCasing.None,
    SchemaCasing = ElementCasing.None,
    TableCasing = ElementCasing.None,
    ColumnCasing = ElementCasing.None,
    VariableCasing = ElementCasing.None
};
```

This preserves the original casing of all elements (no transformation).

## Backward Compatibility

Granular casing is **opt-in**. If no `ElementCasing` properties are set, the formatter falls back to legacy behavior using `KeywordCasing` and `IdentifierCasing` properties:

```csharp
// Legacy behavior (still supported)
var options = new FormattingOptions
{
    KeywordCasing = KeywordCasing.Upper,           // Keywords only
    IdentifierCasing = IdentifierCasing.Preserve   // All identifiers preserved
};
```

To enable granular casing, set at least one `ElementCasing` property:

```csharp
// Enables granular casing with defaults for unset properties
var options = new FormattingOptions
{
    KeywordElementCasing = ElementCasing.Upper  // Only setting keywords enables granular mode
};
```

## Implementation Details

### Token Categorization

The formatter uses Microsoft ScriptDom's token stream with custom categorization logic:

1. **Keywords**: Detected by token type (excluding identifiers, comments, literals)
2. **Functions**: Matched against a built-in function list (COUNT, SUM, GETDATE, etc.)
3. **Data Types**: Matched against a data type list (INT, VARCHAR, DATETIME, etc.)
4. **Schemas**: Identifiers followed by a dot (`.`)
5. **Tables**: Identifiers in FROM/JOIN/INTO/UPDATE clauses or after schema dot
6. **Columns**: All other identifiers (most common case)
7. **Variables**: Tokens starting with `@` or `@@`

### Context Tracking

The categorizer maintains parsing state to correctly identify:
- Multiple tables in a FROM clause: `FROM users, orders, customers`
- Table aliases: `FROM users u, orders o`
- Column aliases: `COUNT(*) AS total`
- Schema-qualified names: `dbo.Users`, `sys.Objects`

### Preserved Elements

The following elements are **never** transformed:
- String literals: `'SELECT FROM WHERE'`
- Comments: `-- select from where`
- Quoted identifiers: `[Order]`, `"User"`, `[Table Name]`

## Architecture

### Components

- **SqlElementCategorizer**: Categorizes tokens into element types
- **ScriptDomElementCaser**: Applies casing transformations
- **CasingHelpers**: Utility functions for case transformations
- **FormattingOptions**: Configuration for casing preferences

### Files

- `src/TsqlRefine.Formatting/FormattingOptions.cs` - Configuration types
- `src/TsqlRefine.Formatting/Helpers/SqlElementCategorizer.cs` - Token categorization
- `src/TsqlRefine.Formatting/Helpers/ScriptDomElementCaser.cs` - Casing application
- `src/TsqlRefine.Formatting/Helpers/CasingHelpers.cs` - Casing utilities
- `tests/TsqlRefine.Formatting.Tests/Helpers/ScriptDomElementCaserTests.cs` - Unit tests

## Testing

Run granular casing tests:

```powershell
dotnet test tests/TsqlRefine.Formatting.Tests --filter "FullyQualifiedName~ScriptDomElementCaserTests"
```

All 19 granular casing tests verify:
- Individual element type casing (keywords, functions, types, etc.)
- Table and column aliases
- Comprehensive multi-element queries
- Preservation of original case with `ElementCasing.None`
- Quoted identifiers and literals

### Example 5: PascalCase Style

```csharp
var options = new FormattingOptions
{
    KeywordElementCasing = ElementCasing.Pascal,
    BuiltInFunctionCasing = ElementCasing.Pascal,
    DataTypeCasing = ElementCasing.Pascal,
    SchemaCasing = ElementCasing.Pascal,
    TableCasing = ElementCasing.Pascal,
    ColumnCasing = ElementCasing.Pascal,
    VariableCasing = ElementCasing.Pascal
};
```

**Input:**
```sql
SELECT user_name, COUNT(*) FROM dbo.users WHERE active = 1;
```

**Output:**
```sql
Select UserName, Count(*) From Dbo.Users Where Active = 1;
```

PascalCase splits on underscores and capitalizes each word: `user_name` → `UserName`, `select` → `Select`.

## Future Enhancements

Potential future additions:
- Per-function casing (treat window functions differently)
- Custom element lists (user-defined functions, custom types)
- IDE integration (VS Code extension settings)

## Migration Guide

### From Legacy Casing

**Before** (legacy):
```csharp
var options = new FormattingOptions
{
    KeywordCasing = KeywordCasing.Upper,
    IdentifierCasing = IdentifierCasing.Lower
};
```

**After** (granular, equivalent behavior):
```csharp
var options = new FormattingOptions
{
    KeywordElementCasing = ElementCasing.Upper,
    BuiltInFunctionCasing = ElementCasing.Upper,  // Functions were keywords before
    DataTypeCasing = ElementCasing.Lower,
    SchemaCasing = ElementCasing.Lower,
    TableCasing = ElementCasing.Lower,
    ColumnCasing = ElementCasing.Lower,
    VariableCasing = ElementCasing.Lower
};
```

### Enabling Granular Casing

Set **at least one** `ElementCasing` property to enable granular mode. Unset properties will use recommended defaults.

**Minimal enable**:
```csharp
var options = new FormattingOptions
{
    KeywordElementCasing = ElementCasing.Upper  // Enables granular mode
    // All other elements use recommended defaults
};
```

**Full control**:
```csharp
var options = new FormattingOptions
{
    KeywordElementCasing = ElementCasing.Upper,
    BuiltInFunctionCasing = ElementCasing.Upper,
    DataTypeCasing = ElementCasing.Lower,
    SchemaCasing = ElementCasing.Lower,
    TableCasing = ElementCasing.Upper,
    ColumnCasing = ElementCasing.Upper,
    VariableCasing = ElementCasing.Lower
};
```
