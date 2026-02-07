---
paths:
  - "src/TsqlRefine.Core/**/*.cs"
  - "src/TsqlRefine.PluginSdk/**/*.cs"
  - "tests/TsqlRefine.Core.Tests/**/*.cs"
---

# Core and PluginSdk Layer Development

Development patterns for TsqlRefine.Core (analysis engine) and TsqlRefine.PluginSdk (contracts).

## PluginSdk (Foundation Layer)

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

### Stability Requirements

**PluginSdk is a public API** - changes require careful consideration:
- Changes require `PluginApi.CurrentVersion` bump
- Avoid breaking changes to existing interfaces
- New features should be additive (new interfaces, not modified existing)
- Document all public types with XML comments

## Core (Engine Layer)

Orchestrates rule execution against SQL inputs.

### Key Components

**TsqlRefineEngine**: Main analysis orchestrator
- Accepts `SqlInput[]` and `IRule[]`
- Runs `ScriptDomTokenizer` to parse SQL
- Executes each rule's `Analyze()` method
- Collects and normalizes diagnostics
- Returns `LintResult`

**ScriptDomTokenizer**: Wraps Microsoft's T-SQL parser
- Maps compat level (100-170) to appropriate TSql parser
- Extracts both AST (full syntax tree) and tokens (flat stream)
- Handles parse errors gracefully

**EngineOptions**: Execution config (severity threshold, ruleset, compat level)

### Data Flow

```
SQL Input → ScriptDomTokenizer → AST + Tokens →
For each rule: Analyze(RuleContext) → Diagnostics →
Collect → Filter by severity → LintResult
```

## SQL Parser Integration

Uses **Microsoft.SqlServer.TransactSql.ScriptDom** for T-SQL parsing.

### Compat Level Mapping

| Level | SQL Server Version | Parser Class |
|-------|-------------------|--------------|
| 100 | SQL Server 2008 | TSql100Parser |
| 110 | SQL Server 2012 | TSql110Parser |
| 120 | SQL Server 2014 | TSql120Parser |
| 130 | SQL Server 2016 | TSql130Parser |
| 140 | SQL Server 2017 | TSql140Parser |
| 150 | SQL Server 2019 | TSql150Parser |
| 160 | SQL Server 2022 | TSql160Parser |

### Updating ScriptDom Parser

When adding support for a new SQL Server version:

1. Update compat level mapping in `ScriptDomTokenizer.cs`
2. Add new parser version case in `GetParser()` method
3. Update documentation with supported versions
4. Test with SQL features specific to new version

### Dual Representation

Rules get both:
- **AST** (full syntax tree): For structural analysis
- **Tokens** (flat stream): For fast pattern matching

```csharp
public sealed record RuleContext(
    string FilePath,
    int CompatLevel,           // 100-160 (SQL Server 2008-2022)
    ScriptDomAst Ast,          // Parsed AST (TSqlFragment)
    IReadOnlyList<Token> Tokens, // Flat token stream
    RuleSettings Settings       // Per-rule config
);
```

## SQL Parsing Constraints

**What IS supported**:
- `GO` batch separator
- DDL statements (CREATE PROC, etc.)
- All T-SQL syntax for configured compat level

**What is NOT analyzed**:
- Dynamic SQL strings (`EXEC(...)`) - treated as opaque text
- String literal content - preserved but not parsed
- Comments - preserved during formatting

## Configuration Loading

### Config Types

- `TsqlRefineConfig`: Main configuration (tsqlrefine.json)
- `Ruleset`: Rule enable/disable settings
- `EngineOptions`: Runtime execution options

### Load Order

CLI args → tsqlrefine.json → defaults

Higher priority settings override lower priority.

## Testing Core

Test patterns for Core layer:

```csharp
[Fact]
public void Engine_WithViolation_ReturnsDiagnostics()
{
    var input = new SqlInput("test.sql", "SELECT * FROM users");
    var rules = new[] { new AvoidSelectStarRule() };
    var options = new EngineOptions();

    var result = TsqlRefineEngine.Run(new[] { input }, rules, options);

    Assert.Single(result.Files);
    Assert.NotEmpty(result.Files[0].Diagnostics);
}
```

## Reference Files

- Engine: `src/TsqlRefine.Core/TsqlRefineEngine.cs`
- Tokenizer: `src/TsqlRefine.Core/ScriptDomTokenizer.cs`
- SDK interfaces: `src/TsqlRefine.PluginSdk/Rules.cs`
- Diagnostics: `src/TsqlRefine.PluginSdk/Diagnostics.cs`
- Configuration: `docs/configuration.md`
