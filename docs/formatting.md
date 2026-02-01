# Formatter Specification

This document describes the specification and usage of the `TsqlRefine.Formatting` module.

---

## 1. Overview

`TsqlRefine.Formatting` is a module that provides minimal formatting for T-SQL code.

**Design Philosophy**:
- **Minimal Formatting**: Only keyword casing and whitespace normalization
- **Structure Preservation**: Does not modify comments, string literals, or code structure
- **Configurable**: Independent control over casing for each element
- **Extensible**: New formatting passes can be added

---

## 2. Formatting Pipeline

SQL input is processed in the following order:

```
SQL Input
    ↓
1. Element-wise Casing (ScriptDomElementCaser)
    - Uses ScriptDom token stream
    - Categorizes keywords, functions, data types, identifiers
    - Applies upper/lower case per category
    ↓
2. Whitespace Normalization (WhitespaceNormalizer)
    - Line ending normalization (CRLF → LF)
    - Indentation normalization (.editorconfig compatible)
    - Trailing whitespace removal (optional)
    - Final newline insertion (optional)
    ↓
3. Inline Space Normalization (InlineSpaceNormalizer)
    - Add space after comma (a,b → a, b)
    - Remove duplicate spaces (SELECT  * → SELECT *)
    - Protected regions unchanged
    ↓
4. Comma Style Transformation (CommaStyleTransformer, optional)
    - Convert trailing commas to leading commas
    ↓
Formatted SQL Output
```

---

## 3. Formatting Options

### 3.1 FormattingOptions

| Property | Type | Default | Description |
|-----------|------|--------|------|
| `CompatLevel` | `int` | `150` | SQL Server compatibility level (100-170) *170 requires package update |
| `IndentStyle` | `IndentStyle` | `Spaces` | Indentation style (Tabs/Spaces) |
| `IndentSize` | `int` | `4` | Indentation size (number of spaces or tab width) |
| `KeywordElementCasing` | `ElementCasing` | `Upper` | Keyword casing |
| `BuiltInFunctionCasing` | `ElementCasing` | `Upper` | Built-in function casing |
| `DataTypeCasing` | `ElementCasing` | `Lower` | Data type casing |
| `SchemaCasing` | `ElementCasing` | `None` | Schema name casing (*caution for CS environments) |
| `TableCasing` | `ElementCasing` | `None` | Table name casing (*caution for CS environments) |
| `ColumnCasing` | `ElementCasing` | `None` | Column name casing (*caution for CS environments) |
| `VariableCasing` | `ElementCasing` | `Lower` | Variable casing |
| `CommaStyle` | `CommaStyle` | `Trailing` | Comma style (Trailing/Leading) |
| `MaxLineLength` | `int` | `0` | Maximum line length (0 = unlimited) |
| `InsertFinalNewline` | `bool` | `true` | Insert newline at end of file |
| `TrimTrailingWhitespace` | `bool` | `true` | Remove trailing whitespace from lines |
| `NormalizeInlineSpacing` | `bool` | `true` | Normalize inline spacing |

### 3.2 Enumerations

**IndentStyle**:
```csharp
public enum IndentStyle
{
    Tabs,
    Spaces
}
```

**ElementCasing**:
```csharp
public enum ElementCasing
{
    None,   // No change (preserve original)
    Upper,  // Uppercase
    Lower   // Lowercase
}
```

**CommaStyle**:
```csharp
public enum CommaStyle
{
    Trailing,  // Trailing comma: SELECT a, b, c
    Leading    // Leading comma:
               // SELECT a
               //      , b
               //      , c
}
```

> **Warning**: In case-sensitive collation environments (e.g., Microsoft Fabric Data Warehouse),
> changing identifier casing may break queries.
> Before setting `SchemaCasing`, `TableCasing`, `ColumnCasing` to `Upper` or `Lower`,
> verify the target environment's collation. The default is `None` (preserve original).

---

## 4. Element Categories

`SqlElementCategorizer` classifies T-SQL tokens into the following categories:

| Category | Description | Examples |
|---------|------|-----|
| `Keyword` | SQL keywords | `SELECT`, `FROM`, `WHERE`, `JOIN` |
| `BuiltInFunction` | Built-in functions | `COUNT`, `SUM`, `GETDATE`, `ISNULL` |
| `DataType` | Data types | `INT`, `VARCHAR`, `DATETIME`, `DECIMAL` |
| `Schema` | Schema names | `dbo`, `sys`, `staging` |
| `Table` | Table names/aliases | `users`, `orders`, `u`, `o` |
| `Column` | Column names/aliases | `id`, `name`, `created_at` |
| `Variable` | Variables | `@id`, `@name`, `@@ROWCOUNT` |
| `Other` | Others (literals, operators, etc.) | `'text'`, `123`, `+`, `-` |

### 4.1 Category Determination Logic

1. **Variable**: Tokens starting with `@`, or token types containing `Variable`
2. **Built-in Function**: Known function names followed by `(`
3. **Data Type**: Known data type names
4. **Schema**: Identifiers followed by `.`
5. **Table**: Identifiers after FROM/JOIN/INTO/UPDATE, or after `.`
6. **Column**: Identifiers not matching above
7. **Keyword**: Word tokens not matching above

---

## 5. Protected Regions

The following regions are protected from transformation:

| Region | Start | End | Example |
|------|------|------|-----|
| String literal | `'` | `'` | `'Hello World'` |
| Double-quoted identifier | `"` | `"` | `"Column Name"` |
| Bracketed identifier | `[` | `]` | `[Table Name]` |
| Block comment | `/*` | `*/` | `/* comment */` |
| Line comment | `--` | End of line | `-- comment` |

**Escape Handling**:
- `''` (within single quotes)
- `]]` (within brackets)

---

## 6. Helper Classes

### 6.1 SqlFormatter

Main orchestrator. Coordinates the formatting pipeline.

```csharp
using TsqlRefine.Formatting;

var sql = "select id, name from users where active = 1";
var formatted = SqlFormatter.Format(sql);
// Result: "SELECT ID, NAME FROM USERS WHERE ACTIVE = 1\n"
```

### 6.2 ScriptDomElementCaser

Applies element-wise casing using ScriptDom token stream.

```csharp
using TsqlRefine.Formatting.Helpers;

var options = new FormattingOptions
{
    KeywordElementCasing = ElementCasing.Upper,
    ColumnCasing = ElementCasing.Lower
};

var cased = ScriptDomElementCaser.Apply(sql, options, compatLevel: 150);
```

**Features**:
- Compatibility level support (100-160)
- Token type name caching for performance
- Context-aware with previous/next token consideration

### 6.3 SqlElementCategorizer

Classifies T-SQL tokens into element categories.

```csharp
using TsqlRefine.Formatting.Helpers;

var category = SqlElementCategorizer.Categorize(token, previousToken, nextToken, context);
```

**Built-in Functions** (partial):
- Aggregate functions: `COUNT`, `SUM`, `AVG`, `MIN`, `MAX`
- String functions: `LEN`, `SUBSTRING`, `REPLACE`, `CONCAT`
- Date functions: `GETDATE`, `DATEADD`, `DATEDIFF`
- Conversion functions: `CAST`, `CONVERT`, `TRY_CAST`
- NULL handling: `ISNULL`, `COALESCE`, `NULLIF`
- Ranking functions: `ROW_NUMBER`, `RANK`, `DENSE_RANK`

**Data Types** (partial):
- Numeric: `INT`, `BIGINT`, `DECIMAL`, `FLOAT`
- String: `CHAR`, `VARCHAR`, `NVARCHAR`, `TEXT`
- Date: `DATE`, `TIME`, `DATETIME`, `DATETIME2`
- Other: `BIT`, `UNIQUEIDENTIFIER`, `XML`

### 6.4 WhitespaceNormalizer

Normalizes whitespace and indentation.

```csharp
using TsqlRefine.Formatting.Helpers;

var normalized = WhitespaceNormalizer.Normalize(sql, options);
```

**Processing**:
- Line ending normalization (`\r\n` → `\n`)
- Indentation reconstruction (tabs/spaces)
- Trailing whitespace removal
- Final newline insertion

### 6.5 InlineSpaceNormalizer

Normalizes inline spacing.

```csharp
using TsqlRefine.Formatting.Helpers;

var normalized = InlineSpaceNormalizer.Normalize(sql, options);
```

**Processing**:
- Add space after commas
- Reduce duplicate spaces to one
- Preserve leading indentation
- Protected regions unchanged

### 6.6 CommaStyleTransformer

Transforms comma style.

```csharp
using TsqlRefine.Formatting.Helpers;

var leading = CommaStyleTransformer.ToLeadingCommas(sql);
```

**Limitations**:
- Line-based simple implementation
- Does not detect commas inside strings/comments
- Does not handle complex nested structures

### 6.7 CasingHelpers

Casing conversion utilities.

```csharp
using TsqlRefine.Formatting.Helpers;

var upper = CasingHelpers.ApplyCasing("select", ElementCasing.Upper);
// Result: "SELECT"
```

### 6.8 ProtectedRegionTracker

Internal class that tracks state inside strings, comments, and brackets.

```csharp
var tracker = new ProtectedRegionTracker();
if (tracker.IsInProtectedRegion())
{
    // Inside protected region
}
```

---

## 7. CLI Usage

### 7.1 Basic Formatting

```powershell
# Output formatted result to stdout
dotnet run --project src/TsqlRefine.Cli -c Release -- format file.sql

# Overwrite file
dotnet run --project src/TsqlRefine.Cli -c Release -- format --write file.sql

# From stdin
echo "select * from users" | dotnet run --project src/TsqlRefine.Cli -c Release -- format --stdin
```

### 7.2 Specifying Options

```powershell
# Specify indent style
dotnet run --project src/TsqlRefine.Cli -c Release -- format --indent-style tabs file.sql

# Specify indent size
dotnet run --project src/TsqlRefine.Cli -c Release -- format --indent-size 2 file.sql

# Combined options
dotnet run --project src/TsqlRefine.Cli -c Release -- format \
    --indent-style spaces \
    --indent-size 4 \
    --write \
    file.sql
```

---

## 8. EditorConfig Support

Settings from `.editorconfig` are automatically loaded:

```ini
[*.sql]
indent_style = spaces  # or tabs
indent_size = 4        # number of spaces
```

**Priority** (highest first):
1. CLI arguments
2. `.editorconfig`
3. `tsqlrefine.json`
4. `FormattingOptions` defaults

---

## 9. Limitations

### 9.1 Formatting Scope

**Supported**:
- Keyword casing
- Identifier casing
- Indentation (spaces/tabs)
- Line ending normalization
- Trailing whitespace removal
- Comma placement (basic cases)

**Not Supported**:
- Query layout reformatting
- Clause reordering
- Expression structure changes
- Adding/removing line breaks (except normalization)
- Adding/removing spaces within expressions

### 9.2 MaxLineLength

Not currently implemented. Requires token-aware line splitting, planned for future implementation.

### 9.3 CommaStyleTransformer

- Line-based simple implementation
- Does not handle complex cases like subqueries, CTEs
- AST-based implementation needed in the future

---

## 10. Performance

- **Parsing**: Uses Microsoft ScriptDom
- **Memory**: Single-pass, StringBuilder-based (minimal allocations)
- **Speed**: ~0.5-2ms for typical queries (<1KB), ~10-50ms for large files (>10KB)
- **Scalability**: Linear with file size

---

## 11. Architecture

### 11.1 Project Structure

```
src/TsqlRefine.Formatting/
├── SqlFormatter.cs              # Orchestrator (26 lines)
├── FormattingOptions.cs         # Options definition
├── TsqlRefine.Formatting.csproj
├── README.md
└── Helpers/
    ├── CasingHelpers.cs         # Casing utilities
    ├── ScriptDomElementCaser.cs # Element-wise casing
    ├── SqlElementCategorizer.cs # Token classification
    ├── WhitespaceNormalizer.cs  # Whitespace normalization
    ├── InlineSpaceNormalizer.cs # Inline space normalization
    ├── CommaStyleTransformer.cs # Comma style transformation
    └── ProtectedRegionTracker.cs # Protected region tracking (internal)
```

### 11.2 Dependencies

```
TsqlRefine.Formatting
    └── Microsoft.SqlServer.TransactSql.ScriptDom
```

---

## 12. Extension Methods

### 12.1 Adding New Formatting Passes

1. Create a new helper class in the `Helpers/` directory
2. Follow the pattern:
   ```csharp
   public static class MyFormattingHelper
   {
       public static string Transform(string input, FormattingOptions options)
       {
           // Implementation
       }
   }
   ```
3. Add to the `SqlFormatter.Format()` pipeline
4. Add tests in `tests/TsqlRefine.Formatting.Tests/Helpers/`

### 12.2 Guidelines

- **Public static classes**: Independently testable, accessible from plugins
- **Single responsibility**: 1 helper = 1 transformation
- **Respect protected regions**: Use `ProtectedRegionTracker` as needed
- **XML documentation**: Document limitations
- **Error handling**: Graceful degradation, don't throw on parse errors

---

## 13. References

- [CLI Specification](cli.md) - How to use the format command
- [Configuration](configuration.md) - Configuration file format
- [Granular Casing](granular-casing.md) - Detailed casing settings
- [Plugin API](plugin-api.md) - Usage from plugins
