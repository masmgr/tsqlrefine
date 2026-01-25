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
# Lint (check) SQL files
dotnet run --project src/TsqlRefine.Cli -c Release -- lint file.sql
dotnet run --project src/TsqlRefine.Cli -c Release -- check file.sql  # alias for lint

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

Current rules (7 total):
1. **AvoidSelectStarRule** (`avoid-select-star`)
   - Category: Performance | Severity: Warning | Fixable: No
   - Detects `SELECT *` usage and recommends explicit column lists

2. **DmlWithoutWhereRule** (`dml-without-where`)
   - Category: Safety | Severity: Error | Fixable: No
   - Detects UPDATE/DELETE statements without WHERE clause

3. **AvoidNullComparisonRule** (`avoid-null-comparison`)
   - Category: Correctness | Severity: Warning | Fixable: No
   - Detects NULL comparisons using `=` or `<>` instead of `IS NULL/IS NOT NULL`

4. **RequireParenthesesForMixedAndOrRule** (`require-parentheses-for-mixed-and-or`)
   - Category: Correctness | Severity: Warning | Fixable: No
   - Detects mixed AND/OR operators without explicit parentheses

5. **AvoidNolockRule** (`avoid-nolock`)
   - Category: Correctness | Severity: Warning | Fixable: No
   - Detects NOLOCK hint or READ UNCOMMITTED isolation level

6. **RequireColumnListForInsertValuesRule** (`require-column-list-for-insert-values`)
   - Category: Correctness | Severity: Warning | Fixable: No
   - Detects INSERT VALUES without explicit column list

7. **RequireColumnListForInsertSelectRule** (`require-column-list-for-insert-select`)
   - Category: Correctness | Severity: Warning | Fixable: No
   - Detects INSERT SELECT without explicit column list

`BuiltinRuleProvider` discovers and exposes all built-in rules.

#### 4. **Formatting (Formatter Layer)**
Independent SQL formatting engine.

`SqlFormatter` uses two-phase approach:
1. `ScriptDomKeywordCaser`: Normalizes keywords to uppercase using ScriptDom tokens
2. `MinimalWhitespaceNormalizer`: Normalizes indentation and whitespace

**EditorConfig Support**: Respects `.editorconfig` settings for:
- `indent_style` (tabs/spaces)
- `indent_size` (number of spaces)

**Constraints**: Preserves comments, string literals, and structure. Minimal reformatting only.

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
Command-line interface built on System.CommandLine.

- `CliApp`: Main dispatcher for all commands
- `CliParser`: Argument parsing
- `CliArgs`: Parsed argument record
- Handles file I/O, glob expansion, output formatting (text/JSON)

**Available commands** (all fully implemented):
- `lint` / `check`: Analyze SQL files for rule violations
- `format`: Format SQL files with keyword casing and whitespace normalization
- `fix`: Auto-fix issues (applies fixes from rules that support it)
- `init`: Create default `tsqlrefine.json` and `tsqlrefine.ignore` files
- `print-config`: Display effective configuration as JSON
- `list-rules`: List all available rules with metadata
- `list-plugins`: List loaded plugins with status information

**Global options**:
- `-c, --config`: Configuration file path
- `-g, --ignorelist`: Ignore patterns file
- `--stdin`: Read from stdin
- `--stdin-filepath`: Set filepath for stdin input
- `--output`: Output format (text/json)
- `--severity`: Minimum severity level (error/warning/info/hint)
- `--preset`: Use preset ruleset (recommended/strict/security-only)
- `--compat-level`: SQL Server compatibility level
- `--ruleset`: Custom ruleset path
- `--write`: Apply changes in-place (format/fix commands)
- `--diff`: Show diff output (format/fix commands)
- `--indent-style`: Indentation style (tabs/spaces)
- `--indent-size`: Indentation size in spaces

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
  Parse SQL → ScriptDomKeywordCaser → MinimalWhitespaceNormalizer →
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

**Sample configurations** in `samples/configs/`:
- `basic.json`: Basic configuration example
- `advanced.json`: Advanced configuration with plugins
- `minimal.json`: Minimal configuration
- `sql-server-2012.json`: SQL Server 2012 specific config

#### Ruleset (enable/disable rules)
JSON schema available at `schemas/ruleset.schema.json`.

```json
{
  "rules": [
    { "id": "avoid-select-star", "enabled": false }
  ]
}
```

**Preset rulesets** in `samples/rulesets/`:
- `recommended.json`: All 7 rules enabled (default)
- `strict.json`: All 7 rules enabled (same as recommended)
- `security-only.json`: Only `dml-without-where` rule enabled
- `custom.json`: Example of selective rule enabling

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
2. Implement `IRule` interface:
   ```csharp
   public class MyRule : IRule
   {
       public RuleMetadata Metadata => new(
           RuleId: "my-rule",
           Description: "...",
           Category: "Performance",
           DefaultSeverity: RuleSeverity.Warning,
           Fixable: false
       );

       public IEnumerable<Diagnostic> Analyze(RuleContext context)
       {
           // Option 1: Token-based pattern matching (fast, simple)
           foreach (var token in context.Tokens)
           {
               if (/* pattern match */)
                   yield return new Diagnostic(...);
           }

           // Option 2: AST visitor pattern (structural analysis)
           var visitor = new MyVisitor();
           context.Ast.Fragment.Accept(visitor);
           foreach (var diagnostic in visitor.Diagnostics)
               yield return diagnostic;
       }

       public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
       {
           // Return fixes if Fixable = true
           // Can return multiple fix options (rule selects best one)
           yield break;
       }
   }
   ```
3. Add to `BuiltinRuleProvider.GetRules()`
4. Add tests in `tests/TsqlRefine.Rules.Tests/`
5. Add sample SQL in `samples/sql/` to demonstrate the rule

**Rule implementation patterns**:
- **Token-based**: Fast pattern matching (e.g., `AvoidSelectStarRule`)
- **AST visitor**: Structural analysis using `TSqlFragmentVisitor` (e.g., `DmlWithoutWhereRule`)
- Use `context.Tokens` for keyword/operator patterns
- Use `context.Ast` for statement structure analysis

### Adding Tests

Tests follow xUnit conventions:
- Core tests: `tests/TsqlRefine.Core.Tests/`
- Rule tests: `tests/TsqlRefine.Rules.Tests/`
- CLI tests: `tests/TsqlRefine.Cli.Tests/`

Run single test:
```powershell
dotnet test --filter "FullyQualifiedName~MyTestName"
```

### Creating a Plugin

1. Create new .NET library project
2. Reference `TsqlRefine.PluginSdk`
3. Implement `IRuleProvider`:
   ```csharp
   public class MyRuleProvider : IRuleProvider
   {
       public IEnumerable<IRule> GetRules()
       {
           yield return new MyCustomRule();
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

## Documentation

Key docs in `docs/`:
- [requirements.md](docs/requirements.md): Project scope and feature requirements
- [rules.md](docs/rules.md): Rule categories, priorities, and presets
- [cli.md](docs/cli.md): CLI specification (JSON output, exit codes)
- [plugin-api.md](docs/plugin-api.md): Plugin API contract
- [project-structure.md](docs/project-structure.md): Detailed project organization

Sample files in `samples/`:
- `configs/`: Configuration file examples (basic, advanced, minimal, sql-server-2012)
- `rulesets/`: Preset ruleset files (recommended, strict, security-only, custom)
- `sql/`: SQL examples demonstrating each rule violation
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
Output formatting is in `CliApp.cs`:
- Text output: `FormatTextOutput()`
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
- Plugin API must remain stable; changes require version bumping
- ScriptDom is an external dependency - we cannot modify its AST structure
- Exit codes are part of the public contract for CI integration
- All CLI commands are fully implemented and functional
- The fix system infrastructure is complete but no rules currently support auto-fixing (all have `Fixable: false`)
- Use `.claude/` directory for agent-specific instructions and configurations
