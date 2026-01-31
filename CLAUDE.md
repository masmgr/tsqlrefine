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

# Print effective formatting options
dotnet run --project src/TsqlRefine.Cli -c Release -- print-format-config
dotnet run --project src/TsqlRefine.Cli -c Release -- print-format-config --show-sources  # with source info
dotnet run --project src/TsqlRefine.Cli -c Release -- print-format-config --output json   # JSON output
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
  lint               Analyze SQL files for rule violations (default)
  format             Format SQL files (keyword casing, whitespace)
  fix                Auto-fix issues that support fixing
  init               Initialize configuration files
  print-config       Print effective configuration
  print-format-config  Print effective formatting options
  list-rules         List available rules
  list-plugins       List loaded plugins
```

**Options by command**:

| Option | lint | format | fix | init | print-config | print-format-config | list-rules | list-plugins |
|--------|:----:|:------:|:---:|:----:|:------------:|:-------------------:|:----------:|:------------:|
| `-c, --config` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `-g, --ignorelist` | ✓ | ✓ | ✓ | - | - | - | - | - |
| `--detect-encoding` | ✓ | ✓ | ✓ | - | - | - | - | - |
| `--stdin` | ✓ | ✓ | ✓ | - | - | - | - | - |
| `--output` | ✓ | ✓ | ✓ | - | ✓ | ✓ | ✓ | ✓ |
| `--severity` | ✓ | - | ✓ | - | - | - | - | - |
| `--preset` | ✓ | - | ✓ | - | - | - | - | - |
| `--compat-level` | ✓ | ✓ | ✓ | - | - | - | - | - |
| `--ruleset` | ✓ | - | ✓ | - | - | - | - | - |
| `--write` | - | ✓ | ✓ | - | - | - | - | - |
| `--diff` | - | ✓ | ✓ | - | - | - | - | - |
| `--indent-style` | - | ✓ | - | - | - | ✓ | - | - |
| `--indent-size` | - | ✓ | - | - | - | ✓ | - | - |
| `--show-sources` | - | - | - | - | - | ✓ | - | - |
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

## Additional Rules for Claude

詳細な開発ガイドラインは `.claude/rules/` ディレクトリに分割されています:

- [development-patterns.md](.claude/rules/development-patterns.md): ルール・フォーマッター・テスト・プラグインの追加方法
- [technical-constraints.md](.claude/rules/technical-constraints.md): 技術的制約と開発上の注意点
- [common-tasks.md](.claude/rules/common-tasks.md): よくあるタスクの手順
