using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects STRING_AGG whose first argument is not explicitly cast to NVARCHAR(MAX),
/// which risks intermediate result truncation (8000-byte / 4000-char limit).
/// </summary>
public sealed class StringAggNvarcharMaxRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "string-agg-nvarchar-max",
        Description: "Detects STRING_AGG whose first argument is not explicitly cast to NVARCHAR(MAX), which risks intermediate result truncation (8000-byte / 4000-char limit).",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
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

        var visitor = new StringAggNvarcharMaxVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class StringAggNvarcharMaxVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(FunctionCall node)
        {
            if (node.FunctionName.Value.Equals("STRING_AGG", StringComparison.OrdinalIgnoreCase)
                && node.Parameters.Count >= 1
                && !IsNvarcharMaxConversion(node.Parameters[0]))
            {
                AddDiagnostic(
                    fragment: node.Parameters[0],
                    message: "STRING_AGG first argument should be explicitly cast to NVARCHAR(MAX) to avoid intermediate result truncation (8000-byte / 4000-char limit).",
                    code: "string-agg-nvarchar-max",
                    category: "Correctness",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        /// <summary>
        /// Determines whether the expression is an explicit CAST/CONVERT to NVARCHAR(MAX).
        /// </summary>
        private static bool IsNvarcharMaxConversion(ScalarExpression expression)
        {
            var dataType = expression switch
            {
                CastCall cast => cast.DataType,
                ConvertCall convert => convert.DataType,
                TryCastCall tryCast => tryCast.DataType,
                TryConvertCall tryConvert => tryConvert.DataType,
                _ => null
            };

            return dataType is SqlDataTypeReference { SqlDataTypeOption: SqlDataTypeOption.NVarChar } sqlType
                && sqlType.Parameters.Count == 1
                && sqlType.Parameters[0].LiteralType == LiteralType.Max;
        }
    }
}
