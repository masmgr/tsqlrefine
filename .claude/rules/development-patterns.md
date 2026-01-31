# Development Patterns

## Adding a New Built-in Rule

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

## Adding a New Formatting Pass

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

## Adding Tests

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

## Creating a Plugin

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
