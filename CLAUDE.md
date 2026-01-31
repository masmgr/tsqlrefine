# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**tsqlrefine** is a SQL Server/T-SQL linter, static analyzer, and formatter written in C#. It provides:
- Lint and static analysis for T-SQL code (SQL Server 2012+)
- Minimal SQL formatting (keyword casing, whitespace normalization)
- Plugin system for custom rules
- CLI tool and library for integration

## Build and Development Commands

### Build
```powershell
# Build entire solution
dotnet build src/TsqlRefine.sln -c Release

# Build specific project
dotnet build src/TsqlRefine.Cli -c Release
```

### Test
```powershell
# Run all tests
dotnet test src/TsqlRefine.sln -c Release

# Run tests for specific project
dotnet test tests/TsqlRefine.Core.Tests -c Release
dotnet test tests/TsqlRefine.Rules.Tests -c Release
dotnet test tests/TsqlRefine.Cli.Tests -c Release

# Run with verbose output
dotnet test -c Release --logger "console;verbosity=detailed"
```

### Run CLI
```powershell
# Lint SQL files
dotnet run --project src/TsqlRefine.Cli -c Release -- lint file.sql

# Lint with JSON output
dotnet run --project src/TsqlRefine.Cli -c Release -- lint --output json file.sql

# Lint from stdin
"SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin

# Lint with severity filtering
dotnet run --project src/TsqlRefine.Cli -c Release -- lint --severity error file.sql

# Format SQL
dotnet run --project src/TsqlRefine.Cli -c Release -- format file.sql
dotnet run --project src/TsqlRefine.Cli -c Release -- format --write file.sql  # in-place
dotnet run --project src/TsqlRefine.Cli -c Release -- format --diff file.sql   # show diff

# Auto-fix issues
dotnet run --project src/TsqlRefine.Cli -c Release -- fix file.sql
dotnet run --project src/TsqlRefine.Cli -c Release -- fix --write file.sql    # apply fixes
dotnet run --project src/TsqlRefine.Cli -c Release -- fix --diff file.sql     # show diff

# Initialize configuration
dotnet run --project src/TsqlRefine.Cli -c Release -- init

# List available rules
dotnet run --project src/TsqlRefine.Cli -c Release -- list-rules

# List loaded plugins
dotnet run --project src/TsqlRefine.Cli -c Release -- list-plugins

# Print effective configuration
dotnet run --project src/TsqlRefine.Cli -c Release -- print-config
```

## Architecture

### Project Structure

The codebase follows a layered architecture with clear separation of concerns:

```
src/
├── TsqlRefine.PluginSdk/     # Contracts and interfaces (foundation)
├── TsqlRefine.Core/          # Analysis engine and tokenizer
├── TsqlRefine.Rules/         # Built-in rules
│   ├── Helpers/              # Shared utilities for rule implementation
│   └── Rules/                # Individual rule implementations
├── TsqlRefine.Formatting/    # SQL formatter
├── TsqlRefine.PluginHost/    # Plugin loading infrastructure
└── TsqlRefine.Cli/           # Command-line interface
```

**Dependency flow**: `Cli` → `Core`/`Formatting`/`PluginHost`/`Rules` → `PluginSdk`

### Core Concepts

#### 1. **PluginSdk (Foundation Layer)**
The contract layer that defines all public interfaces and data types. **Zero dependencies**.

Key types:
- `IRule`: Interface all rules must implement (Analyze + GetFixes)
- `IRuleProvider`: Discovers rules in an assembly
- `RuleContext`: Data passed to rules (AST, tokens, file path, compat level, settings)
- `RuleMetadata`: Rule identity (ID, category, severity, fixability)
- `Diagnostic`: Issue report (range, message, severity, code)
- `Fix` / `TextEdit`: Auto-fix proposals
- `ScriptDomAst`: Wrapper around Microsoft ScriptDom's TSqlFragment
- `Token`: SQL token with type and location

#### 2. **Core (Engine Layer)**
Orchestrates rule execution against SQL inputs.

Key components:
- `TsqlRefineEngine`: Main analysis orchestrator
  - Accepts `SqlInput[]` and `IRule[]`
  - Runs `ScriptDomTokenizer` to parse SQL
  - Executes each rule's `Analyze()` method
  - Collects and normalizes diagnostics
  - Returns `LintResult`
- `ScriptDomTokenizer`: Wraps Microsoft's T-SQL parser
  - Maps compat level (100-160) to appropriate TSql parser
  - Extracts both AST (full syntax tree) and tokens (flat stream)
  - Handles parse errors gracefully
- `EngineOptions`: Execution config (severity threshold, ruleset, compat level)
- Config types: `TsqlRefineConfig`, `Ruleset`

#### 3. **Rules (Built-in Rules)**
Ships with the tool. Each rule implements `IRule` interface.

`BuiltinRuleProvider` discovers and exposes all 85 built-in rules.

**Rule Helper Classes** (in `src/TsqlRefine.Rules/Helpers/`):

All rules leverage shared helper utilities to reduce code duplication:

1. **ScriptDomHelpers** - Static utilities for AST fragment operations
   - `GetRange(TSqlFragment)`: Converts ScriptDom coordinates (1-based) to PluginSdk Range (0-based)
   - Used by all AST-based rules to calculate diagnostic ranges

2. **TokenHelpers** - Static utilities for token stream analysis
   - `IsKeyword(Token, string)`: Case-insensitive keyword matching
   - `IsTrivia(Token)`: Detects whitespace and comments
   - `IsPrefixedByDot(IReadOnlyList<Token>, int)`: Checks for qualified identifiers
   - `GetTokenEnd(Token)`: Calculates token end position (handles multi-line tokens)
   - Used by token-based rules like `AvoidSelectStarRule`

3. **DiagnosticVisitorBase** - Abstract base class for AST visitors
   - Extends `TSqlFragmentVisitor` with diagnostic collection
   - Provides `AddDiagnostic()` methods for creating diagnostics
   - Manages `Diagnostics` collection automatically

4. **RuleHelpers** - Common rule patterns
   - `NoFixes(RuleContext, Diagnostic)`: Standard implementation for non-fixable rules

5. **DatePartHelper** - Identifies T-SQL date/time functions with datepart literals
   - `IsDatePartFunction(FunctionCall)`: Checks for DATEADD, DATEDIFF, DATEPART, DATENAME
   - `IsDatePartLiteralParameter(...)`: Detects datepart literal parameters to avoid false positives

6. **ExpressionAnalysisHelpers** - Expression analysis utilities
   - `ContainsColumnReference(ScalarExpression)`: Checks if expression contains column references
   - Handles nested CAST, CONVERT, FunctionCall, BinaryExpression

7. **PredicateAwareVisitorBase** - Visitor with predicate context tracking
   - Extends `DiagnosticVisitorBase` with `IsInPredicate` property
   - Tracks WHERE, JOIN ON, and HAVING clause contexts

8. **TableReferenceHelpers** - Table reference utilities
   - `CollectTableReferences(...)`: Recursively collects leaf table references from JOINs
   - `CollectTableAliases(...)`: Collects all declared table aliases/names
   - `GetAliasOrTableName(TableReference)`: Gets alias or base table name

9. **TextAnalysisHelpers** - Raw text analysis utilities
   - `SplitSqlLines(string)`: Splits SQL handling all line endings (CRLF, CR, LF)
   - `CreateLineRangeDiagnostic(...)`: Creates diagnostics for specific lines

**Benefits**: These 9 helpers provide single source of truth for common operations and are available to external plugins.

#### 4. **Formatting (Formatter Layer)**
Independent SQL formatting engine with composable passes.

`SqlFormatter` orchestrates formatting through a 4-step pipeline:
1. `ScriptDomElementCaser`: Granular element casing (keywords, functions, data types, schemas, tables, columns, variables)
2. `WhitespaceNormalizer`: Indentation and whitespace (respects .editorconfig)
3. `InlineSpaceNormalizer`: Inline spacing normalization (space after commas, remove duplicate spaces)
4. `CommaStyleTransformer`: Comma style transformation (trailing to leading, optional)

**Helper Classes** (in `src/TsqlRefine.Formatting/Helpers/`):
- `ScriptDomElementCaser`: Granular element-based casing with token categorization
- `SqlElementCategorizer`: Categorizes tokens into Keyword, BuiltInFunction, DataType, Schema, Table, Column, Variable
- `WhitespaceNormalizer`: Public, testable whitespace normalization
- `InlineSpaceNormalizer`: Adds space after commas, removes duplicate spaces
- `CommaStyleTransformer`: Public comma style transformation (trailing to leading)
- `CasingHelpers`: Casing transformations (eliminates duplication)
- `ProtectedRegionTracker`: State machine for strings/comments/brackets (internal)
- All public helpers are available to plugins

**EditorConfig Support**: Respects `.editorconfig` for indentation
**Constraints**: Minimal formatting only, preserves comments/strings/structure

**Architecture**: 49-line orchestrator + 7 independently testable helpers

#### 5. **PluginHost (Plugin Runtime)**
Dynamically loads external rule plugins at runtime.

- `PluginLoader`: Loads DLL plugins via reflection
- `PluginLoadContext`: Custom AssemblyLoadContext for isolation
  - Cross-platform native DLL loading (Windows .dll, Linux .so, macOS .dylib)
  - Supports `runtimes/<rid>/native` pattern for platform-specific libraries
- `PluginDescriptor`: Plugin metadata (path, enabled flag)
- Supports API versioning (`PluginApi.CurrentVersion = 1`)
- Graceful error handling (plugin failures don't crash core)
- Detailed diagnostic information for plugin load failures

#### 6. **Cli (User Interface)**
Command-line interface built on System.CommandLine 2.0.0.

- `CliApp`: Main dispatcher for all commands
- `CliParser`: Subcommand-based argument parsing with typed options per command
- `CliArgs`: Parsed argument record
- Handles file I/O, glob expansion, output formatting (text/JSON)
- `--help` and `--version` are handled automatically by System.CommandLine (auto-generated help per subcommand)

**Command structure** (subcommand-based):
```
tsqlrefine <command> [options] [paths...]

Commands:
  lint          Analyze SQL files for rule violations (default)
  format        Format SQL files (keyword casing, whitespace)
  fix           Auto-fix issues that support fixing
  init          Initialize configuration files
  print-config  Print effective configuration
  list-rules    List available rules
  list-plugins  List loaded plugins
```

**Options by command**:

| Option | lint | format | fix | init | print-config | list-rules | list-plugins |
|--------|:----:|:------:|:---:|:----:|:------------:|:----------:|:------------:|
| `-c, --config` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `-g, --ignorelist` | ✓ | ✓ | ✓ | - | - | - | - |
| `--detect-encoding` | ✓ | ✓ | ✓ | - | - | - | - |
| `--stdin` | ✓ | ✓ | ✓ | - | - | - | - |
| `--stdin-filepath` | ✓ | ✓ | ✓ | - | - | - | - |
| `--output` | ✓ | ✓ | ✓ | - | ✓ | ✓ | ✓ |
| `--severity` | ✓ | - | ✓ | - | - | - | - |
| `--preset` | ✓ | - | ✓ | - | - | - | - |
| `--compat-level` | ✓ | ✓ | ✓ | - | - | - | - |
| `--ruleset` | ✓ | - | ✓ | - | - | - | - |
| `--write` | - | ✓ | ✓ | - | - | - | - |
| `--diff` | - | ✓ | ✓ | - | - | - | - |
| `--indent-style` | - | ✓ | - | - | - | - | - |
| `--indent-size` | - | ✓ | - | - | - | - | - |
| `--verbose` | - | - | - | - | - | - | ✓ |
| `paths...` | ✓ | ✓ | ✓ | - | - | - | - |

### Data Flow

#### Lint/Check Command Flow
```
CLI args → Parse → Load config → Load rules (builtin + plugins) →
Create Engine → For each SQL input:
  Parse SQL (ScriptDom) → AST + Tokens →
  For each rule: Analyze(context) → Diagnostics →
Collect results → Format output (text/JSON) → Exit code
```

#### Format Command Flow
```
CLI args → Parse → Load .editorconfig → For each SQL input:
  Parse SQL → ScriptDomElementCaser → WhitespaceNormalizer →
  InlineSpaceNormalizer → CommaStyleTransformer (optional) →
Output (stdout/file/diff) → Exit
```

#### Fix Command Flow
```
CLI args → Parse → Load config → Load rules (builtin + plugins) →
Create Engine → For each SQL input:
  Parse SQL → AST + Tokens →
  For each rule: Analyze(context) → Diagnostics →
  For each diagnostic: GetFixes(context, diagnostic) → Fix[] →
  Select best fix per diagnostic → Detect overlaps →
  Apply non-overlapping fixes → Re-analyze to verify →
Output (stdout/file/diff) → Report applied/skipped fixes → Exit
```

### Rule Execution Model

Each rule receives a `RuleContext`:
```csharp
public sealed record RuleContext(
    string FilePath,
    int CompatLevel,           // 100-160 (SQL Server 2008-2022)
    ScriptDomAst Ast,          // Parsed AST (TSqlFragment)
    IReadOnlyList<Token> Tokens, // Flat token stream
    RuleSettings Settings       // Per-rule config
);
```

Rules return `Diagnostic[]` with:
- `Range`: Position in source (0-based line/character)
- `Message`: Human-readable description
- `Severity`: Error/Warning/Information/Hint
- `Code`: Rule ID (e.g., "avoid-select-star")
- `Data`: Metadata (category, fixable flag)

### SQL Parser Integration

Uses **Microsoft.SqlServer.TransactSql.ScriptDom** for T-SQL parsing.

**Compat level mapping**:
- 100 → SQL Server 2008
- 110 → SQL Server 2012
- 120 → SQL Server 2014
- 150 → SQL Server 2019
- 160 → SQL Server 2022

**Dual representation**: Rules get both AST (for structural analysis) and tokens (for fast pattern matching).

### Configuration

#### tsqlrefine.json
JSON schema available at `schemas/tsqlrefine.schema.json`.

```json
{
  "compatLevel": 150,              // SQL Server compat level (100, 110, 120, 150, 160)
  "ruleset": "rulesets/recommended.json", // Optional ruleset file
  "plugins": [
    { "path": "plugins/custom.dll", "enabled": true }
  ]
}
```

**Sample configurations**:
- `samples/config/tsqlrefine.json`: Default configuration example
- `samples/config/advanced.json`: Advanced configuration with plugins
- `samples/config/minimal.json`: Minimal configuration
- `samples/config/sql-server-2012.json`: SQL Server 2012 specific config
- `samples/configs/formatting-options.json`: Formatting options example

#### Ruleset (enable/disable rules)
JSON schema available at `schemas/ruleset.schema.json`.

```json
{
  "rules": [
    { "id": "avoid-select-star", "enabled": false }
  ]
}
```

**Preset rulesets** in `rulesets/`:
- `recommended.json`: Balanced production use (49 rules) - High-value rules with minimal false positives
- `strict.json`: Maximum enforcement (86 rules) - All rules except demonstrably incorrect ones
- `pragmatic.json`: Production-ready minimum (30 rules) - Prevent bugs and data loss, minimize style noise
- `security-only.json`: Security and critical safety (10 rules) - Prevent security vulnerabilities and destructive operations

**Config load order**: CLI args → tsqlrefine.json → defaults

#### .editorconfig
Format command respects `.editorconfig` for indentation settings:
```ini
[*.sql]
indent_style = spaces  # or tabs
indent_size = 4        # number of spaces
```

### Exit Codes

Defined in `ExitCodes.cs`:
- `0`: Success (no violations or filtered to zero)
- `1`: Rule violations found
- `2`: Parse error (syntax error, GO batch split failure)
- `3`: Config error (invalid config, bad compat level)
- `4`: Runtime exception (internal error)

## Development Patterns

### Adding a New Built-in Rule

1. Create rule class in `src/TsqlRefine.Rules/Rules/`
2. Implement `IRule` interface using helper classes:

   **Option 1: AST-based rule (recommended for structural analysis)**
   ```csharp
   using Microsoft.SqlServer.TransactSql.ScriptDom;
   using TsqlRefine.PluginSdk;
   using TsqlRefine.Rules.Helpers;

   namespace TsqlRefine.Rules.Rules;

   public sealed class MyRule : IRule
   {
       public RuleMetadata Metadata { get; } = new(
           RuleId: "my-rule",
           Description: "...",
           Category: "Performance",
           DefaultSeverity: RuleSeverity.Warning,
           Fixable: false
       );

       public IEnumerable<Diagnostic> Analyze(RuleContext context)
       {
           ArgumentNullException.ThrowIfNull(context);

           if (context.Ast.Fragment is null)
           {
               yield break;
           }

           var visitor = new MyVisitor();
           context.Ast.Fragment.Accept(visitor);

           foreach (var diagnostic in visitor.Diagnostics)
           {
               yield return diagnostic;
           }
       }

       public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
           RuleHelpers.NoFixes(context, diagnostic);

       // Visitor extends DiagnosticVisitorBase for automatic diagnostic collection
       private sealed class MyVisitor : DiagnosticVisitorBase
       {
           public override void ExplicitVisit(SomeStatementType node)
           {
               if (/* condition */)
               {
                   // Use AddDiagnostic helper - no need for GetRange()
                   AddDiagnostic(
                       fragment: node,
                       message: "Your diagnostic message",
                       code: "my-rule",
                       category: "Performance",
                       fixable: false
                   );
               }

               base.ExplicitVisit(node);
           }
       }
   }
   ```

   **Option 2: Token-based rule (for pattern matching)**
   ```csharp
   using TsqlRefine.PluginSdk;
   using TsqlRefine.Rules.Helpers;

   namespace TsqlRefine.Rules.Rules;

   public sealed class MyTokenRule : IRule
   {
       public RuleMetadata Metadata { get; } = new(
           RuleId: "my-token-rule",
           Description: "...",
           Category: "Performance",
           DefaultSeverity: RuleSeverity.Warning,
           Fixable: false
       );

       public IEnumerable<Diagnostic> Analyze(RuleContext context)
       {
           ArgumentNullException.ThrowIfNull(context);

           for (var i = 0; i < context.Tokens.Count; i++)
           {
               // Use TokenHelpers for common operations
               if (TokenHelpers.IsKeyword(context.Tokens[i], "SELECT"))
               {
                   // Skip trivia
                   if (TokenHelpers.IsTrivia(context.Tokens[i]))
                       continue;

                   // Check for qualified identifiers
                   if (TokenHelpers.IsPrefixedByDot(context.Tokens, i))
                       continue;

                   // Calculate range
                   var start = context.Tokens[i].Start;
                   var end = TokenHelpers.GetTokenEnd(context.Tokens[i]);

                   yield return new Diagnostic(
                       Range: new TsqlRefine.PluginSdk.Range(start, end),
                       Message: "Your message",
                       Code: "my-token-rule",
                       Data: new DiagnosticData("my-token-rule", "Performance", false)
                   );
               }
           }
       }

       public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
           RuleHelpers.NoFixes(context, diagnostic);
   }
   ```

3. Add to `BuiltinRuleProvider.GetRules()`
4. Add tests in `tests/TsqlRefine.Rules.Tests/`
5. Add sample SQL in `samples/sql/` to demonstrate the rule

**Rule implementation patterns**:
- **AST visitor**: Structural analysis - extend `DiagnosticVisitorBase` and use `AddDiagnostic()` helper
- **Token-based**: Fast pattern matching - use `TokenHelpers` utilities
- **Always use helper classes**: `ScriptDomHelpers`, `TokenHelpers`, `DiagnosticVisitorBase`, `RuleHelpers`
- **Never duplicate code**: GetRange(), IsKeyword(), etc. are provided by helpers

### Adding a New Formatting Pass

To add a new formatting transformation:

1. Create helper class in `src/TsqlRefine.Formatting/Helpers/`
2. Follow the established pattern:
   - **Public static class** with descriptive name
   - Single public method: `public static string Transform(string input, FormattingOptions options)` or similar
   - XML documentation with constraints and limitations
   - TODO comments for known limitations
3. Add to `SqlFormatter.Format()` pipeline in appropriate order
4. Add unit tests in `tests/TsqlRefine.Formatting.Tests/Helpers/`
5. Update existing integration tests if behavior changes
6. Document in `src/TsqlRefine.Formatting/README.md`

**Example**:
```csharp
namespace TsqlRefine.Formatting.Helpers;

/// <summary>
/// Performs [description of transformation].
///
/// Known limitations:
/// - [limitation 1]
/// - [limitation 2]
/// </summary>
public static class MyFormattingHelper
{
    public static string Transform(string input, FormattingOptions options)
    {
        // Implementation
        // Use ProtectedRegionTracker if you need to avoid transforming strings/comments
    }
}
```

**Available helpers to leverage**:
- `ProtectedRegionTracker`: State machine for strings, comments, brackets
- `CasingHelpers`: Common casing transformations
- `ScriptDomElementCaser`: For granular element-based casing transformations
- `SqlElementCategorizer`: For categorizing tokens into element types
- `InlineSpaceNormalizer`: For inline spacing normalization

### Adding Tests

Tests follow xUnit conventions:
- Core tests: `tests/TsqlRefine.Core.Tests/`
- Rule tests: `tests/TsqlRefine.Rules.Tests/`
  - Rule tests: `tests/TsqlRefine.Rules.Tests/*RuleTests.cs`
  - Helper tests: `tests/TsqlRefine.Rules.Tests/Helpers/*Tests.cs`
- CLI tests: `tests/TsqlRefine.Cli.Tests/`
- Formatting tests: `tests/TsqlRefine.Formatting.Tests/`

Run single test:
```powershell
dotnet test --filter "FullyQualifiedName~MyTestName"
```

**Helper class tests**:
When adding or modifying helper utilities in `src/TsqlRefine.Rules/Helpers/`, add corresponding tests in `tests/TsqlRefine.Rules.Tests/Helpers/`:
- `ScriptDomHelpersTests.cs` - Tests for GetRange() and AST utilities
- `TokenHelpersTests.cs` - Tests for token analysis utilities
- `DiagnosticVisitorBaseTests.cs` - Tests for visitor base class
- `RuleHelpersTests.cs` - Tests for common rule patterns
- `DatePartHelperTests.cs` - Tests for date part function detection
- `ExpressionAnalysisHelpersTests.cs` - Tests for expression analysis
- `PredicateAwareVisitorBaseTests.cs` - Tests for predicate context tracking
- `TableReferenceHelpersTests.cs` - Tests for table reference utilities
- `TextAnalysisHelpersTests.cs` - Tests for raw text analysis

### Creating a Plugin

1. Create new .NET library project
2. Reference `TsqlRefine.PluginSdk` (for interfaces) and optionally `TsqlRefine.Rules` (for helper utilities)
3. Implement `IRuleProvider`:
   ```csharp
   using TsqlRefine.PluginSdk;
   using TsqlRefine.Rules.Helpers;  // Optional: Use built-in helpers

   public class MyRuleProvider : IRuleProvider
   {
       public string Name => "My Custom Rules";
       public int PluginApiVersion => 1;

       public IReadOnlyList<IRule> GetRules()
       {
           return new IRule[]
           {
               new MyCustomRule()
           };
       }
   }

   // Your custom rule can use the same helpers as built-in rules
   public class MyCustomRule : IRule
   {
       public RuleMetadata Metadata { get; } = new(
           RuleId: "my-custom-rule",
           Description: "My custom rule description",
           Category: "Custom",
           DefaultSeverity: RuleSeverity.Warning,
           Fixable: false
       );

       public IEnumerable<Diagnostic> Analyze(RuleContext context)
       {
           // Use DiagnosticVisitorBase, TokenHelpers, etc.
           var visitor = new MyVisitor();
           context.Ast.Fragment?.Accept(visitor);
           return visitor.Diagnostics;
       }

       public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
           RuleHelpers.NoFixes(context, diagnostic);

       private sealed class MyVisitor : DiagnosticVisitorBase
       {
           public override void ExplicitVisit(SelectStatement node)
           {
               AddDiagnostic(
                   fragment: node,
                   message: "Custom rule violation",
                   code: "my-custom-rule",
                   category: "Custom",
                   fixable: false
               );
               base.ExplicitVisit(node);
           }
       }
   }
   ```
4. Build as DLL
5. Configure in `tsqlrefine.json`:
   ```json
   {
     "plugins": [
       { "path": "./plugins/MyPlugin.dll", "enabled": true }
     ]
   }
   ```

**Note**: Plugins can leverage `TsqlRefine.Rules.Helpers` utilities (ScriptDomHelpers, TokenHelpers, DiagnosticVisitorBase, RuleHelpers) to avoid duplicating common code patterns.

## Documentation

Key docs in `docs/`:
- [cli.md](docs/cli.md): CLI specification (JSON output, exit codes)
- [configuration.md](docs/configuration.md): Configuration file format and options
- [formatting.md](docs/formatting.md): Formatting options and behavior
- [granular-casing.md](docs/granular-casing.md): Granular element casing documentation
- [inline-disable.md](docs/inline-disable.md): Inline comment-based rule disabling
- [plugin-api.md](docs/plugin-api.md): Plugin API contract
- [project-structure.md](docs/project-structure.md): Detailed project organization
- [release.md](docs/release.md): Release process and notes
- [task-list.md](docs/task-list.md): Development task tracking

Sample files in `samples/`:
- `config/`: Configuration file examples (tsqlrefine.json, advanced, minimal, sql-server-2012)
- `configs/`: Additional configuration examples (formatting-options.json)
- `rulesets/`: Preset ruleset files (recommended, strict, pragmatic, security-only)
- `sql/`: SQL examples demonstrating each rule violation
- `sql/inline-disable/`: Examples of inline disable directives
- `README.md`: Comprehensive guide to samples

## Technical Constraints

### Target Framework
- .NET 10.0 (see `global.json` for SDK version)
- C# with nullable reference types enabled

### Code Style
- EditorConfig enforced (`.editorconfig`)
- 4-space indentation for C#
- LF line endings
- UTF-8 encoding

### SQL Parsing Constraints
- Supports `GO` batch separator
- DDL statements (CREATE PROC, etc.) supported
- Dynamic SQL strings (`EXEC(...)`) and string literals are NOT analyzed (treated as opaque text)
- Comments and string content preserved during formatting

### Formatting Philosophy
**Minimal formatting only**:
- Keyword casing normalization (uppercase)
- Identifier casing (with escaping for reserved words like `[Order]`)
- Whitespace normalization (respects .editorconfig settings)
- **Preserves**: Comments, string literals, parenthesis-internal line breaks
- **Does NOT**: Reformat layout, reorder clauses, change structure

### Fix System
Auto-fix infrastructure for rules that support fixing:
- Multiple fixes per diagnostic supported (rule selects best one)
- Overlap detection prevents conflicting edits
- Line-by-line text mapping for position-to-offset conversion
- Re-analysis after fixes to verify remaining issues
- Detailed reporting: `AppliedFix` vs `SkippedFix` with reasons

## Common Tasks

### Update ScriptDom Parser
When adding support for a new SQL Server version:
1. Update compat level mapping in `ScriptDomTokenizer.cs`
2. Add new parser version case in `GetParser()` method
3. Update documentation with supported versions

### Modify CLI Commands
Commands are dispatched in `CliApp.RunAsync()`. To add a command:
1. Add command definition in `CliParser.cs`
2. Add handler method in `CliApp.cs`
3. Update help text and documentation

### Change Output Format
Output formatting is in `CommandExecutor.cs`:
- Text output: `file:line:col: Severity: Message (rule-id)` format (ESLint/GCC style)
- JSON output: Uses System.Text.Json with `JsonDefaults.cs` options

### Debug Rule Execution
Add logging or breakpoints in:
- `TsqlRefineEngine.Run()`: Main execution loop
- `ScriptDomTokenizer.Analyze()`: Parsing stage
- Individual rule's `Analyze()` method

### Initialize New Project
Run the `init` command to create default configuration:
```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- init
```
This creates:
- `tsqlrefine.json`: Default configuration file
- `tsqlrefine.ignore`: Default ignore patterns file

## Notes for Claude

- Japanese documentation is present (`docs/` files) - important architectural details are there
- When modifying rules, always add corresponding tests
- When adding new rules, create sample SQL files in `samples/sql/` to demonstrate violations
- **Always use helper classes** when implementing rules:
  - Use `DiagnosticVisitorBase` for AST-based rules (not `TSqlFragmentVisitor` directly)
  - Use `ScriptDomHelpers.GetRange()` instead of duplicating range calculation
  - Use `TokenHelpers` utilities for token analysis
  - Use `RuleHelpers.NoFixes()` for non-fixable rules
- Plugin API must remain stable; changes require version bumping
- ScriptDom is an external dependency - we cannot modify its AST structure
- Exit codes are part of the public contract for CI integration
- All CLI commands are fully implemented and functional
- The fix system infrastructure is complete but no rules currently support auto-fixing (all have `Fixable: false`)
- Use `.claude/` directory for agent-specific instructions and configurations
- Helper classes in `TsqlRefine.Rules.Helpers` are public and available to external plugins
