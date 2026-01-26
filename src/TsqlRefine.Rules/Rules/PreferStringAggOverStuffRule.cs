using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class PreferStringAggOverStuffRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "prefer-string-agg-over-stuff",
        Description: "Recommends STRING_AGG() over STUFF(... FOR XML PATH('') ...); simpler and typically faster/safer (SQL Server 2017+).",
        Category: "Modernization",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // STRING_AGG is available in SQL Server 2017+ (CompatLevel 140+)
        if (context.CompatLevel < 140)
        {
            yield break;
        }

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new PreferStringAggOverStuffVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class PreferStringAggOverStuffVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(FunctionCall node)
        {
            // Check for STUFF function
            if (node.FunctionName.Value.Equals("STUFF", StringComparison.OrdinalIgnoreCase))
            {
                // Check if any parameter contains FOR XML PATH pattern
                if (node.Parameters != null)
                {
                    foreach (var param in node.Parameters)
                    {
                        if (ContainsForXmlPath(param))
                        {
                            AddDiagnostic(
                                fragment: node,
                                message: "Use STRING_AGG() instead of STUFF with FOR XML PATH; it's simpler, faster, and safer.",
                                code: "prefer-string-agg-over-stuff",
                                category: "Modernization",
                                fixable: false
                            );
                            break;
                        }
                    }
                }
            }

            base.ExplicitVisit(node);
        }


        private static bool ContainsForXmlPath(ScalarExpression expression)
        {
            // Check if expression contains a subquery with FOR XML PATH
            if (expression is ScalarSubquery subquery)
            {
                return HasForXmlPath(subquery.QueryExpression);
            }

            return false;
        }

        private static bool HasForXmlPath(QueryExpression queryExpression)
        {
            if (queryExpression is QuerySpecification querySpec)
            {
                return querySpec.ForClause != null;
            }

            return false;
        }
    }
}
