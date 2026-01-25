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
# Run CLI directly (from project)
dotnet run --project src/TsqlRefine.Cli -c Release -- lint file.sql

# Lint with JSON output
dotnet run --project src/TsqlRefine.Cli -c Release -- lint --output json file.sql

# Lint from stdin
"SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin

# Format SQL
dotnet run --project src/TsqlRefine.Cli -c Release -- format file.sql

# List available rules
dotnet run --project src/TsqlRefine.Cli -c Release -- list-rules
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

Current rules:
- `AvoidSelectStarRule`: Detects `SELECT *` usage (Performance/Warning)

`BuiltinRuleProvider` discovers and exposes all built-in rules.

#### 4. **Formatting (Formatter Layer)**
Independent SQL formatting engine.

`SqlFormatter` uses two-phase approach:
1. `ScriptDomKeywordCaser`: Normalizes keywords to uppercase using ScriptDom tokens
2. `MinimalWhitespaceNormalizer`: Normalizes indentation and whitespace

**Constraints**: Preserves comments, string literals, and structure. Minimal reformatting only.

#### 5. **PluginHost (Plugin Runtime)**
Dynamically loads external rule plugins at runtime.

- `PluginLoader`: Loads DLL plugins via reflection
- `PluginLoadContext`: Custom AssemblyLoadContext for isolation
- `PluginDescriptor`: Plugin metadata (path, enabled flag)
- Supports API versioning (`PluginApi.CurrentVersion = 1`)
- Graceful error handling (plugin failures don't crash core)

#### 6. **Cli (User Interface)**
Command-line interface built on System.CommandLine.

- `CliApp`: Main dispatcher for all commands
- `CliParser`: Argument parsing
- `CliArgs`: Parsed argument record
- Handles file I/O, glob expansion, output formatting (text/JSON)

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
CLI args → Parse → For each SQL input:
  Parse SQL → ScriptDomKeywordCaser → MinimalWhitespaceNormalizer →
Output (stdout/file/diff) → Exit
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
```json
{
  "compatLevel": 150,              // SQL Server compat level
  "ruleset": "rulesets/recommended.json", // Optional ruleset file
  "plugins": [
    { "path": "plugins/custom.dll", "enabled": true }
  ]
}
```

#### Ruleset (enable/disable rules)
```json
{
  "rules": [
    { "id": "avoid-select-star", "enabled": false }
  ]
}
```

**Preset rulesets** in `rulesets/`:
- `recommended.json`: Default ruleset
- `strict.json`: More aggressive rules
- `security-only.json`: Security-focused rules

**Config load order**: CLI args → tsqlrefine.json → defaults

### Exit Codes

Defined in `ExitCodes.cs`:
- `0`: Success (no violations or filtered to zero)
- `1`: Rule violations found
- `2`: Parse error (syntax error, GO batch split failure)
- `3`: Config error (invalid config, bad compat level)
- `4`: Runtime exception (internal error)

## Development Patterns

### Adding a New Built-in Rule

1. Create rule class in `src/TsqlRefine.Rules/`
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
           // Scan context.Tokens or traverse context.Ast
           // Yield diagnostics for violations
       }

       public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
       {
           // Return fixes if Fixable = true
           yield break;
       }
   }
   ```
3. Add to `BuiltinRuleProvider.GetRules()`
4. Add tests in `tests/TsqlRefine.Rules.Tests/`

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
- Keyword casing normalization
- Identifier casing (with escaping for reserved words like `[Order]`)
- Whitespace normalization
- **Preserves**: Comments, string literals, parenthesis-internal line breaks
- **Does NOT**: Reformat layout, reorder clauses, change structure

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

## Notes for Claude

- Japanese documentation is present (`docs/` files) - important architectural details are there
- The project is in active development; some features are not yet implemented (e.g., `fix` command)
- When modifying rules, always add corresponding tests
- Plugin API must remain stable; changes require version bumping
- ScriptDom is an external dependency - we cannot modify its AST structure
- Exit codes are part of the public contract for CI integration
