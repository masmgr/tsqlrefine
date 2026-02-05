---
name: tsql-rule-implementer
description: Implement T-SQL linter rules using ScriptDom AST analysis with TDD methodology. Use when: creating new linting rules, implementing rule detection logic, writing rule tests, or registering rules in BuiltinRuleProvider.
---

# T-SQL Rule Implementer

Implement linting rules using TDD methodology.

## Workflow

1. **Write tests first** in `tests/TsqlRefine.Rules.Tests/{RuleName}RuleTests.cs`
2. **Implement rule** in `src/TsqlRefine.Rules/Rules/{RuleName}Rule.cs`
3. **Register rule** in `src/TsqlRefine.Rules/BuiltinRuleProvider.cs`
4. **Run tests**: `dotnet test --filter "FullyQualifiedName~{RuleName}Tests"`
5. **Verify**: `dotnet test src/TsqlRefine.sln -c Release`

## Rule Class Template

```csharp
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class {RuleName}Rule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "{rule-id}",
        Description: "...",
        Category: "{Category}",
        DefaultSeverity: RuleSeverity.{Severity},
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Ast.Fragment is null) yield break;

        var visitor = new {RuleName}Visitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
            yield return diagnostic;
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class {RuleName}Visitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit({TargetNode} node)
        {
            if (ShouldReport(node))
            {
                AddDiagnostic(
                    fragment: node,
                    message: "...",
                    code: "{rule-id}",
                    category: "{Category}",
                    fixable: false
                );
            }
            base.ExplicitVisit(node);
        }
    }
}
```

## Naming Conventions

| Type | Pattern | Example |
|------|---------|---------|
| Rule Class | PascalCase + "Rule" | `AvoidSelectStarRule` |
| Test Class | PascalCase + "RuleTests" | `AvoidSelectStarRuleTests` |
| Rule ID | kebab-case | `avoid-select-star` |
| Visitor | Private nested + "Visitor" | `AvoidSelectStarVisitor` |

## Key Files

| Purpose | Path |
|---------|------|
| Rule pattern | `src/TsqlRefine.Rules/Rules/AvoidSelectStarRule.cs` |
| Test pattern | `tests/TsqlRefine.Rules.Tests/AvoidSelectStarRuleTests.cs` |
| Interfaces | `src/TsqlRefine.PluginSdk/Rules.cs` |
| Registration | `src/TsqlRefine.Rules/BuiltinRuleProvider.cs` |
| Visitor base | `src/TsqlRefine.Rules/Helpers/DiagnosticVisitorBase.cs` |

## Common AST Nodes

| Node | Use Case |
|------|----------|
| `UpdateStatement`, `DeleteStatement` | DML without WHERE |
| `BooleanComparisonExpression` + `NullLiteral` | NULL comparison |
| `SelectStarExpression` | SELECT * detection |
| `NamedTableReference` + `TableHint` | NOLOCK detection |
| `QualifiedJoin` | JOIN analysis |

## Verification

```powershell
# Run specific tests
dotnet test --filter "FullyQualifiedName~{RuleName}Tests"

# Full test suite
dotnet test src/TsqlRefine.sln -c Release

# CLI smoke test
echo "UPDATE users SET active=1;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --output json
```
