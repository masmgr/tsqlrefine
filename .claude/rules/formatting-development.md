---
paths:
  - "src/TsqlRefine.Formatting/**/*.cs"
  - "tests/TsqlRefine.Formatting.Tests/**/*.cs"
---

# Formatting Layer Development

Development patterns for the TsqlRefine.Formatting project - SQL formatter implementation.

## Formatting Philosophy

**Minimal formatting only** - the formatter intentionally limits its scope:

**Does**:
- Keyword casing normalization (uppercase)
- Identifier casing (with escaping for reserved words like `[Order]`)
- Whitespace normalization (respects .editorconfig)
- Space after commas, remove duplicate spaces

**Does NOT**:
- Reformat layout or line breaks
- Reorder clauses
- Change SQL structure
- Touch content inside strings or comments

## Formatter Pipeline

`SqlFormatter.Format()` orchestrates a 4-step pipeline:

1. **ScriptDomElementCaser** - Granular element casing (keywords, functions, data types, schemas, tables, columns, variables)
2. **WhitespaceNormalizer** - Indentation and whitespace (respects .editorconfig)
3. **InlineSpaceNormalizer** - Inline spacing (space after commas, remove duplicate spaces)
4. **CommaStyleTransformer** - Comma style transformation (trailing to leading, optional)

Each pass is independently testable with single responsibility.

## Adding a New Formatting Pass

### Step 1: Create Helper Class

Create `src/TsqlRefine.Formatting/Helpers/{PassName}.cs`:

```csharp
namespace TsqlRefine.Formatting.Helpers;

/// <summary>
/// Performs [description of transformation].
///
/// Known limitations:
/// - [limitation 1]
/// - [limitation 2]
/// </summary>
public static class MyFormattingPass
{
    public static string Transform(string input, FormattingOptions options)
    {
        // Implementation
        // Use ProtectedRegionTracker if you need to avoid transforming strings/comments
    }
}
```

### Step 2: Add to Pipeline

Add to `SqlFormatter.Format()` in appropriate order.

### Step 3: Add Tests

Create tests in `tests/TsqlRefine.Formatting.Tests/Helpers/{PassName}Tests.cs`.

### Step 4: Update Documentation

Document in `src/TsqlRefine.Formatting/README.md`.

## Helper Classes

### ProtectedRegionTracker (Internal)

State machine for strings, comments, and brackets - use this to avoid transforming protected content:

```csharp
var tracker = new ProtectedRegionTracker();
foreach (var ch in input)
{
    tracker.Update(ch);
    if (!tracker.IsProtected)
    {
        // Safe to transform this character
    }
}
```

### CasingHelpers

Common casing transformations:
- `ToUpperCase(string)`: Converts to uppercase
- `ToLowerCase(string)`: Converts to lowercase
- `ToPascalCase(string)`: Converts to PascalCase

### ScriptDomElementCaser

Granular element-based casing using ScriptDom token categorization:
- Categorizes tokens into: Keyword, BuiltInFunction, DataType, Schema, Table, Column, Variable
- Applies appropriate casing per element type

### SqlElementCategorizer

Categorizes SQL tokens into element types:
- `Categorize(TSqlTokenType, string)`: Returns `SqlElementCategory`
- Categories: Keyword, BuiltInFunction, DataType, Schema, Table, Column, Variable, Other

### WhitespaceNormalizer

Indentation and whitespace normalization:
- Respects `.editorconfig` settings (indent_style, indent_size)
- Normalizes leading whitespace per line

### InlineSpaceNormalizer

Inline spacing:
- Adds space after commas
- Removes duplicate spaces
- Preserves protected regions (strings, comments)

### CommaStyleTransformer

Comma style transformation:
- Transforms trailing commas to leading
- Preserves SQL semantics
- Optional pass (enabled by configuration)

## EditorConfig Support

Format command respects `.editorconfig` for indentation:

```ini
[*.sql]
indent_style = spaces  # or tabs
indent_size = 4        # number of spaces
```

Configuration precedence: CLI args > `.editorconfig` > defaults

## Testing Patterns

Test each formatting pass independently:

```csharp
[Fact]
public void Transform_InputCase_ExpectedOutput()
{
    var input = "select * from users";
    var options = new FormattingOptions();

    var result = MyFormattingPass.Transform(input, options);

    Assert.Equal("SELECT * FROM users", result);
}
```

Key test scenarios:
- Basic transformation
- Protected regions (strings, comments)
- Multi-line input
- Edge cases (empty input, whitespace only)

## Reference Files

- Main formatter: `src/TsqlRefine.Formatting/SqlFormatter.cs`
- Helpers: `src/TsqlRefine.Formatting/Helpers/`
- Tests: `tests/TsqlRefine.Formatting.Tests/Helpers/`
- Documentation: `docs/formatting.md`, `docs/granular-casing.md`
