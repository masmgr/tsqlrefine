using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// Recommends TRIM(x) instead of LTRIM(RTRIM(x)); clearer and less error-prone (SQL Server 2017+).
/// </summary>
public sealed class PreferTrimOverLtrimRtrimRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "prefer-trim-over-ltrim-rtrim",
        Description: "Recommends TRIM(x) instead of LTRIM(RTRIM(x)); clearer and less error-prone (SQL Server 2017+).",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Check CompatLevel - TRIM is available in SQL Server 2017+ (CompatLevel 140+)
        if (context.CompatLevel < 140)
        {
            yield break;
        }

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new PreferTrimOverLtrimRtrimVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class PreferTrimOverLtrimRtrimVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(FunctionCall node)
        {
            var functionName = node.FunctionName.Value;

            // Check for LTRIM(RTRIM(...)) or RTRIM(LTRIM(...))
            if (functionName.Equals("LTRIM", StringComparison.OrdinalIgnoreCase) ||
                functionName.Equals("RTRIM", StringComparison.OrdinalIgnoreCase))
            {
                if (node.Parameters.Count == 1 && node.Parameters[0] is FunctionCall innerFunc)
                {
                    var innerName = innerFunc.FunctionName.Value;
                    bool isNestedTrimPattern = (functionName.Equals("LTRIM", StringComparison.OrdinalIgnoreCase) &&
                                               innerName.Equals("RTRIM", StringComparison.OrdinalIgnoreCase)) ||
                                              (functionName.Equals("RTRIM", StringComparison.OrdinalIgnoreCase) &&
                                               innerName.Equals("LTRIM", StringComparison.OrdinalIgnoreCase));

                    if (isNestedTrimPattern)
                    {
                        AddDiagnostic(
                            fragment: node,
                            message: $"Use TRIM() instead of nested {functionName.ToUpperInvariant()}({innerName.ToUpperInvariant}()); it's clearer and less error-prone.",
                            code: "prefer-trim-over-ltrim-rtrim",
                            category: "Style",
                            fixable: false
                        );
                    }
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
