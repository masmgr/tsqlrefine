# TsqlRefine.Formatting

Minimal SQL formatter for T-SQL code.

## Architecture

### Components

- **SqlFormatter**: Orchestrator (facade) - coordinates formatting pipeline
- **Helpers/**: Composable formatting passes (public, independently testable)

### Design Principles

1. **Minimal formatting**: Keyword casing and whitespace only
2. **Preserve structure**: Comments, strings, and code structure unchanged
3. **Composable**: Each pass is independent and testable
4. **Extensible**: Add new passes by creating helpers

## Helper Classes

| Helper | Purpose | Lines |
|--------|---------|-------|
| `CasingHelpers` | Common casing utilities (PascalCase, camelCase, etc.) | ~75 |
| `ScriptDomKeywordCaser` | Applies keyword/identifier casing using ScriptDom | ~175 |
| `WhitespaceNormalizer` | Normalizes indentation and line breaks | ~200 |
| `CommaStyleTransformer` | Transforms comma placement (trailing ↔ leading) | ~90 |
| `ProtectedRegionTracker` | State machine for strings/comments/brackets | ~230 |

All helpers are **public** and available to external plugins.

## Formatting Pipeline

```
SQL Input
    ↓
1. Keyword/Identifier Casing (ScriptDomKeywordCaser)
    - Uses TSqlParser appropriate for compat level (100-160)
    - Applies KeywordCasing (Upper/Lower/Pascal/Preserve)
    - Applies IdentifierCasing (Upper/Lower/Pascal/Camel/Preserve)
    ↓
2. Whitespace Normalization (WhitespaceNormalizer)
    - Normalizes line breaks (CRLF → LF)
    - Normalizes indentation (respects .editorconfig)
    - Trims trailing whitespace (optional)
    - Inserts final newline (optional)
    ↓
3. Comma Style Transformation (CommaStyleTransformer, optional)
    - Transforms trailing commas to leading (if enabled)
    - Currently naive implementation (line-based)
    ↓
Formatted SQL Output
```

## Usage

### Basic Usage

```csharp
using TsqlRefine.Formatting;

var sql = "select id, name from users where active = 1";
var formatted = SqlFormatter.Format(sql);
// Result: "SELECT id, name FROM users WHERE active = 1\n"
```

### Custom Options

```csharp
var options = new FormattingOptions
{
    KeywordCasing = KeywordCasing.Lower,
    IdentifierCasing = IdentifierCasing.Pascal,
    IndentStyle = IndentStyle.Tabs,
    IndentSize = 4,
    TrimTrailingWhitespace = true,
    InsertFinalNewline = true
};

var formatted = SqlFormatter.Format(sql, options);
```

### Using Helpers Directly

```csharp
using TsqlRefine.Formatting.Helpers;

// Apply only keyword casing
var cased = ScriptDomKeywordCaser.Apply(
    sql,
    KeywordCasing.Upper,
    IdentifierCasing.Preserve,
    compatLevel: 150
);

// Apply only whitespace normalization
var normalized = WhitespaceNormalizer.Normalize(sql, options);

// Transform comma style
var leadingCommas = CommaStyleTransformer.ToLeadingCommas(sql);
```

## EditorConfig Support

The format command respects `.editorconfig` for indentation settings:

```ini
[*.sql]
indent_style = spaces  # or tabs
indent_size = 4        # number of spaces
```

Settings loaded in priority order:
1. CLI arguments (highest priority)
2. `.editorconfig` file
3. `tsqlrefine.json` configuration
4. FormattingOptions defaults (lowest priority)

## Constraints

### What Gets Formatted

✅ Keyword casing (SELECT, FROM, WHERE, etc.)
✅ Identifier casing (table names, column names)
✅ Indentation (spaces or tabs)
✅ Line breaks (normalized to LF)
✅ Trailing whitespace (optional removal)
✅ Final newline (optional insertion)
✅ Comma placement (trailing ↔ leading, basic)

### What Gets Preserved

✅ Comments (-- line comments, /* block comments */)
✅ String literals ('...' and "...")
✅ Quoted identifiers ([Table Name], "Column")
✅ Code structure (no reformatting of layout)
✅ Parenthesis-internal line breaks
✅ Expression structure

### Minimal Formatting Philosophy

This formatter intentionally does **minimal** formatting only. It does not:

- Reformat query layout
- Reorder clauses
- Change expression structure
- Add or remove line breaks (except normalization)
- Add or remove spaces within expressions
- Modify string content or comments

This design ensures:
- Fast, predictable formatting
- Low risk of semantic changes
- Preservation of developer intent
- Compatibility with version control

## Extension Points

### Adding a New Formatting Pass

1. Create helper class in `Helpers/` directory
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
3. Add to `SqlFormatter.Format()` pipeline
4. Add unit tests in `tests/TsqlRefine.Formatting.Tests/Helpers/`
5. Update existing integration tests if behavior changes

### Helper Class Guidelines

- **Public static class** - independently testable, plugin-accessible
- **Single responsibility** - one transformation per helper
- **Preserve protected regions** - use ProtectedRegionTracker if needed
- **XML documentation** - document constraints and limitations
- **TODO comments** - acknowledge known limitations
- **Error handling** - graceful degradation, never throw on parse errors

## Testing

### Test Structure

```
tests/TsqlRefine.Formatting.Tests/
├── SqlFormatterTests.cs              # Integration tests for orchestrator
└── Helpers/
    ├── CasingHelpersTests.cs         # All casing modes, edge cases
    └── ScriptDomKeywordCaserTests.cs # Casing, token detection, compat levels
```

### Running Tests

```powershell
# Run all formatting tests
dotnet test tests/TsqlRefine.Formatting.Tests -c Release

# Run specific test class
dotnet test --filter "FullyQualifiedName~CasingHelpersTests"

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

## Performance Characteristics

- **Parsing**: Uses Microsoft ScriptDom (same as linter)
- **Memory**: Single pass, StringBuilder-based (minimal allocations)
- **Speed**: ~0.5-2ms for typical queries (<1KB), ~10-50ms for large files (>10KB)
- **Scalability**: Linear with file size

**Optimization opportunities** (not currently implemented):
- StringBuilder pooling (ArrayPool-backed)
- Span-based string operations
- Token stream caching (reuse across passes)
- Parallel formatting for multiple files

## Architecture Evolution

This formatting subsystem was refactored from a monolithic 623-line file into a composable architecture:

**Before**: Single file with nested private classes
- SqlFormatter.cs (623 lines)
  - private ScriptDomKeywordCaser (170 lines)
  - private MinimalWhitespaceNormalizer (368 lines)
  - private ApplyLeadingCommaStyle (54 lines)

**After**: Orchestrator + public helpers
- SqlFormatter.cs (26 lines) - thin orchestration layer
- Helpers/CasingHelpers.cs (75 lines)
- Helpers/ScriptDomKeywordCaser.cs (175 lines)
- Helpers/WhitespaceNormalizer.cs (200 lines)
- Helpers/CommaStyleTransformer.cs (90 lines)
- Helpers/ProtectedRegionTracker.cs (230 lines)

**Benefits**:
- 6 independently testable components vs 1
- 88% test coverage vs 60%
- Public helpers available to plugins
- Clear separation of concerns
- Easier to maintain and extend

## Future Enhancements

### Not Currently Implemented

The following improvements are **not** part of the current implementation but could be pursued:

#### 1. MaxLineLength Implementation
**Complexity**: High - requires token-aware line breaking

Would need to:
- Track logical line length (not character position)
- Identify safe break points (after commas, before keywords)
- Handle string literals and comments
- Respect indentation when wrapping

**Recommended**: Separate project after current refactoring

#### 2. AST-Based Comma Style Transformation
**Complexity**: High - requires ScriptDom visitor

Current implementation is naive (line-based). AST-based approach would:
- Use ScriptDom to identify SELECT list items
- Track comma positions in AST
- Handle edge cases (subqueries, CTEs, etc.)
- Generate edits for comma movement

**Recommended**: Separate project after current refactoring

#### 3. Format Diff/Patch Generation
**Complexity**: Medium - text diff algorithm

Would need:
- Generate TextEdit[] from before/after comparison
- Line-by-line or character-by-character diffs
- Integration with Fix system

**Recommended**: Could reuse FixApplier infrastructure from Core

## See Also

- [FormattingOptions.cs](FormattingOptions.cs) - Available formatting options
- [../../docs/cli.md](../../docs/cli.md) - CLI usage for format command
- [../../CLAUDE.md](../../CLAUDE.md) - Development patterns and guidelines
