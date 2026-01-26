using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class RequireExplicitJoinTypeRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "require-explicit-join-type",
        Description: "Disallows ambiguous JOIN shorthand; makes JOIN semantics explicit and consistent across a codebase.",
        Category: "Query Structure",
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

        var visitor = new RequireExplicitJoinTypeVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireExplicitJoinTypeVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(FromClause node)
        {
            // Check for comma-separated tables (old-style implicit joins)
            if (node.TableReferences.Count > 1)
            {
                // If there are multiple table references at the top level without JOIN keywords,
                // it's an implicit join (comma-separated)
                bool hasImplicitJoin = false;
                foreach (var tableRef in node.TableReferences)
                {
                    if (tableRef is not JoinTableReference)
                    {
                        hasImplicitJoin = true;
                        break;
                    }
                }

                if (hasImplicitJoin && node.TableReferences.Count > 1)
                {
                    AddDiagnostic(
                        fragment: node,
                        message: "Use explicit JOIN syntax instead of comma-separated tables; implicit joins are ambiguous and harder to understand.",
                        code: "require-explicit-join-type",
                        category: "Query Structure",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }

    }
}
