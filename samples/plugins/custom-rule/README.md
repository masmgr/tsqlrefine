# Custom Rule Plugin Sample

This sample demonstrates how to create a custom rule plugin for tsqlrefine.

## Overview

This plugin provides a single example rule: `no-magic-numbers`, which detects numeric literals in SQL code and suggests using named constants or variables instead.

## Project Structure

- **CustomRule.csproj** - .NET project file targeting .NET 10.0
- **CustomRuleProvider.cs** - Plugin entry point implementing `IRuleProvider`
- **NoMagicNumbersRule.cs** - Example rule implementation

## Building the Plugin

```powershell
# From the samples/plugins/custom-rule directory
dotnet build -c Release

# The compiled DLL will be at:
# bin/Release/net10.0/CustomRule.dll
```

## Using the Plugin

### Method 1: Configuration File

Create or update `tsqlrefine.json`:

```json
{
  "compatLevel": 150,
  "plugins": [
    { "path": "samples/plugins/custom-rule/bin/Release/net10.0/CustomRule.dll", "enabled": true }
  ]
}
```

Alternatively, copy the DLL to a search path and reference it by filename only:

```powershell
# Copy to user-wide plugin directory
mkdir -p ~/.tsqlrefine/plugins
cp samples/plugins/custom-rule/bin/Release/net10.0/CustomRule.dll ~/.tsqlrefine/plugins/
```

```json
{
  "plugins": [
    { "path": "CustomRule.dll" }
  ]
}
```

Filename-only paths are searched in: config file directory, `CWD/.tsqlrefine/plugins/`, and `HOME/.tsqlrefine/plugins/`.

Then run:

```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- lint your-file.sql
```

### Method 2: Command Line

```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- lint --config samples/config/advanced.json samples/sql/comprehensive.sql
```

## Creating Your Own Rule

To create a custom rule:

1. **Implement the `IRule` interface**:
   - Define `RuleMetadata` with rule ID, description, category, and severity
   - Implement `Analyze()` to scan SQL and return diagnostics
   - Implement `GetFixes()` if your rule supports auto-fixes

2. **Choose an analysis approach**:
   - **Token-based**: Fast pattern matching on flat token stream (see `NoMagicNumbersRule`)
   - **AST-based**: Structural analysis using ScriptDom's syntax tree (use `context.Ast`)

3. **Register in `IRuleProvider`**:
   - Add your rule to the `GetRules()` method
   - Set `PluginApiVersion = PluginApi.CurrentVersion`

## Rule API Reference

### RuleMetadata

```csharp
public sealed record RuleMetadata(
    string RuleId,           // Unique identifier (e.g., "no-magic-numbers")
    string Description,      // Human-readable description
    string Category,         // Category (e.g., "Style", "Performance", "Security")
    RuleSeverity DefaultSeverity, // Error, Warning, Information, or Hint
    bool Fixable,           // Whether the rule provides auto-fixes
    int? MinCompatLevel = null, // Optional: minimum SQL Server compat level
    int? MaxCompatLevel = null  // Optional: maximum SQL Server compat level
);
```

### RuleContext

The `RuleContext` passed to `Analyze()` contains:

```csharp
public sealed record RuleContext(
    string FilePath,                    // Path to the SQL file being analyzed
    int CompatLevel,                    // SQL Server compat level (100-160)
    ScriptDomAst Ast,                   // Parsed syntax tree (TSqlFragment)
    IReadOnlyList<Token> Tokens,        // Flat token stream
    RuleSettings Settings               // Per-rule configuration (currently empty)
);
```

### Diagnostic

Return diagnostics from `Analyze()`:

```csharp
public sealed record Diagnostic(
    Range Range,                        // Location in source (0-based line/character)
    string Message,                     // Human-readable message
    DiagnosticSeverity? Severity = null, // Error, Warning, Information, Hint
    string? Code = null,                // Rule ID
    string Source = "tsqlrefine",       // Source of diagnostic
    IReadOnlyList<DiagnosticTag>? Tags = null, // Optional tags (Unnecessary, Deprecated)
    DiagnosticData? Data = null         // Metadata (rule ID, category, fixable)
);
```

## Examples

### Token-Based Analysis (Fast)

```csharp
public IEnumerable<Diagnostic> Analyze(RuleContext context)
{
    foreach (var token in context.Tokens)
    {
        if (token.Text == "SELECT" && context.Tokens[index + 1].Text == "*")
        {
            yield return new Diagnostic(/* ... */);
        }
    }
}
```

### AST-Based Analysis (Structural)

```csharp
public IEnumerable<Diagnostic> Analyze(RuleContext context)
{
    var visitor = new MyVisitor();
    context.Ast.Fragment.Accept(visitor);
    return visitor.Diagnostics;
}

private class MyVisitor : TSqlFragmentVisitor
{
    public List<Diagnostic> Diagnostics { get; } = new();

    public override void Visit(SelectStarExpression node)
    {
        Diagnostics.Add(new Diagnostic(/* ... */));
    }
}
```

## Plugin API Versioning

The plugin must declare compatibility with the current Plugin API version:

```csharp
public int PluginApiVersion => PluginApi.CurrentVersion; // Currently 1
```

If the API version doesn't match, tsqlrefine will refuse to load the plugin to prevent compatibility issues.

## Troubleshooting

### Plugin Not Loading

- Check that `PluginApiVersion` matches `PluginApi.CurrentVersion`
- Verify the DLL path in your config is correct (relative or absolute)
- Ensure the plugin targets .NET 10.0
- Check that the plugin references the correct TsqlRefine.PluginSdk version

### Rules Not Running

- Verify your `IRuleProvider` is public and has a parameterless constructor
- Check that `GetRules()` returns your rule instances
- Ensure the plugin is marked as `"enabled": true` in the config

## Further Reading

- [Plugin API Documentation](../../../docs/plugin-api.md)
- [Built-in Rules](../../../src/TsqlRefine.Rules/) - Examples of rule implementations
- [Project Documentation](../../../CLAUDE.md) - Architecture and development guide
