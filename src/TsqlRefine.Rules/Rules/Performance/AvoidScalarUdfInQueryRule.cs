using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Detects user-defined scalar function calls in queries which execute row-by-row and cause severe performance degradation.
/// </summary>
public sealed class AvoidScalarUdfInQueryRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-scalar-udf-in-query",
        Description: "Detects user-defined scalar function calls in queries which execute row-by-row and cause severe performance degradation.",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidScalarUdfInQueryVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidScalarUdfInQueryVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(FunctionCall node)
        {
            // Schema-qualified function calls (e.g., dbo.MyFunc()) have a non-null CallTarget.
            // Built-in functions (GETDATE, UPPER, ISNULL, etc.) have CallTarget == null.
            // Table-valued functions in FROM clause are SchemaObjectFunctionTableReference, not FunctionCall.
            if (node.CallTarget is not null)
            {
                var functionName = BuildFunctionName(node);

                AddDiagnostic(
                    fragment: node,
                    message: $"Avoid calling scalar user-defined function '{functionName}' in queries. Scalar UDFs execute row-by-row, preventing parallelism and causing performance degradation. Consider inline table-valued functions or rewriting the logic inline.",
                    code: "avoid-scalar-udf-in-query",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        private static string BuildFunctionName(FunctionCall node)
        {
            var funcName = node.FunctionName?.Value ?? "function";

            if (node.CallTarget is MultiPartIdentifierCallTarget mpit &&
                mpit.MultiPartIdentifier?.Identifiers is { Count: > 0 } identifiers)
            {
                var schema = string.Join(".", identifiers.Select(id => id.Value));
                return $"{schema}.{funcName}";
            }

            return funcName;
        }
    }
}
