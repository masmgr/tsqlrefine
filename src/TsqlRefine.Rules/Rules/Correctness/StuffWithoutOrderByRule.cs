using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects STUFF with FOR XML PATH that lacks ORDER BY, which may produce non-deterministic string concatenation results.
/// </summary>
public sealed class StuffWithoutOrderByRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "stuff-without-order-by",
        Description: "Detects STUFF with FOR XML PATH that lacks ORDER BY, which may produce non-deterministic string concatenation results.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new StuffWithoutOrderByVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class StuffWithoutOrderByVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(FunctionCall node)
        {
            // Check for STUFF function
            if (node.FunctionName.Value.Equals("STUFF", StringComparison.OrdinalIgnoreCase) &&
                node.Parameters is { Count: > 0 })
            {
                foreach (var param in node.Parameters)
                {
                    if (IsForXmlPathWithoutOrderBy(param, out var columnHint))
                    {
                        var message = string.IsNullOrEmpty(columnHint)
                            ? "STUFF with FOR XML PATH lacks ORDER BY; results may be non-deterministic."
                            : $"STUFF with FOR XML PATH lacks ORDER BY for '{columnHint}'; results may be non-deterministic.";

                        AddDiagnostic(
                            fragment: node.FunctionName,
                            message: message,
                            code: "stuff-without-order-by",
                            category: "Correctness",
                            fixable: false
                        );
                        break;
                    }
                }
            }

            base.ExplicitVisit(node);
        }

        private static bool IsForXmlPathWithoutOrderBy(ScalarExpression expression, out string columnHint)
        {
            columnHint = string.Empty;

            if (expression is not ScalarSubquery subquery)
            {
                return false;
            }

            return CheckQueryExpressionForXmlPath(subquery.QueryExpression, ref columnHint);
        }

        private static bool CheckQueryExpressionForXmlPath(QueryExpression queryExpression, ref string columnHint)
        {
            if (queryExpression is not QuerySpecification querySpec)
            {
                return false;
            }

            // Check if it has FOR XML PATH clause
            if (querySpec.ForClause is not XmlForClause xmlForClause)
            {
                return false;
            }

            // Only check FOR XML PATH (not AUTO, RAW, EXPLICIT)
            if (!IsForXmlPath(xmlForClause))
            {
                return false;
            }

            // Extract column hint from SELECT list for better error message
            columnHint = ExtractColumnHint(querySpec);

            // Check if ORDER BY is missing
            return querySpec.OrderByClause is null;
        }

        private static bool IsForXmlPath(XmlForClause xmlForClause)
        {
            foreach (var option in xmlForClause.Options)
            {
                if (option.OptionKind == XmlForClauseOptions.Path)
                {
                    return true;
                }
            }
            return false;
        }

        private static string ExtractColumnHint(QuerySpecification querySpec)
        {
            if (querySpec.SelectElements is not { Count: > 0 })
            {
                return string.Empty;
            }

            // Try to find a column reference in the SELECT list
            foreach (var element in querySpec.SelectElements)
            {
                if (element is SelectScalarExpression scalarExpr)
                {
                    var columnName = ExtractColumnName(scalarExpr.Expression);
                    if (!string.IsNullOrEmpty(columnName))
                    {
                        return columnName;
                    }
                }
            }

            return string.Empty;
        }

        private static string ExtractColumnName(ScalarExpression expr)
        {
            return expr switch
            {
                ColumnReferenceExpression colRef => colRef.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value ?? string.Empty,
                BinaryExpression binExpr => ExtractColumnName(binExpr.FirstExpression) ?? ExtractColumnName(binExpr.SecondExpression) ?? string.Empty,
                _ => string.Empty
            };
        }
    }
}
