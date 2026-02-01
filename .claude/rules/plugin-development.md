---
paths:
  - "src/TsqlRefine.PluginHost/**/*.cs"
---

# PluginHost Layer Development

Development patterns for TsqlRefine.PluginHost - plugin loading infrastructure.

## Architecture

Dynamically loads external rule plugins at runtime.

Key components:
- `PluginLoader`: Loads DLL plugins via reflection
- `PluginLoadContext`: Custom AssemblyLoadContext for isolation
- `PluginDescriptor`: Plugin metadata (path, enabled flag)

## Plugin Loading Flow

```
Config plugins[] → For each enabled plugin:
  Load DLL → Create PluginLoadContext →
  Find IRuleProvider → Get rules →
  Merge with built-in rules
```

## PluginLoadContext

Custom `AssemblyLoadContext` for plugin isolation:
- Prevents plugin dependencies from conflicting with host
- Cross-platform native DLL loading (Windows .dll, Linux .so, macOS .dylib)
- Supports `runtimes/<rid>/native` pattern for platform-specific libraries

## API Versioning

```csharp
public static class PluginApi
{
    public const int CurrentVersion = 1;
}
```

**Version compatibility**:
- Plugin's `IRuleProvider.PluginApiVersion` must match or be less than `PluginApi.CurrentVersion`
- Version mismatch results in plugin load failure with clear error message
- Bump version when breaking changes occur in PluginSdk

## Error Handling

Plugin failures don't crash core:
- Invalid DLL: Logged, skipped
- Missing IRuleProvider: Logged, skipped
- Version mismatch: Logged, skipped
- Rule execution error: Logged, continues with other rules

Detailed diagnostic information for plugin load failures:
- File path
- Error type
- Exception message
- Expected vs actual API version

## Creating a Plugin

### Step 1: Create .NET Library Project

```powershell
dotnet new classlib -n MyTsqlPlugin
cd MyTsqlPlugin
dotnet add reference path/to/TsqlRefine.PluginSdk.dll
dotnet add reference path/to/TsqlRefine.Rules.dll  # Optional: for helpers
```

### Step 2: Implement IRuleProvider

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
```

### Step 3: Implement Custom Rule

```csharp
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

### Step 4: Build and Configure

Build as DLL:
```powershell
dotnet build -c Release
```

Configure in `tsqlrefine.json`:
```json
{
  "plugins": [
    { "path": "./plugins/MyTsqlPlugin.dll", "enabled": true }
  ]
}
```

## Available Helpers for Plugins

Plugins can reference `TsqlRefine.Rules` to use built-in helpers:
- `DiagnosticVisitorBase`: Base class for AST visitors
- `TokenHelpers`: Token stream analysis
- `ScriptDomHelpers`: AST utilities
- `RuleHelpers`: Common patterns

## Testing Plugin Loading

```csharp
[Fact]
public void PluginLoader_ValidPlugin_LoadsRules()
{
    var descriptor = new PluginDescriptor("path/to/plugin.dll", true);
    var loader = new PluginLoader();

    var rules = loader.Load(descriptor);

    Assert.NotEmpty(rules);
}
```

## Troubleshooting

Common plugin load failures:
- **Missing dependencies**: Ensure all plugin dependencies are in the same directory
- **Version mismatch**: Check `PluginApiVersion` matches `PluginApi.CurrentVersion`
- **Missing IRuleProvider**: Ensure exactly one public class implements `IRuleProvider`
- **Platform-specific DLLs**: Place in `runtimes/<rid>/native/` directory

## Reference Files

- Plugin loader: `src/TsqlRefine.PluginHost/PluginLoader.cs`
- Load context: `src/TsqlRefine.PluginHost/PluginLoadContext.cs`
- API version: `src/TsqlRefine.PluginSdk/PluginApi.cs`
- Documentation: `docs/plugin-api.md`
