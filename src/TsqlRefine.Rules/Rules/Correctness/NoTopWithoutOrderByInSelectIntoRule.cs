using System.Globalization;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects SELECT TOP ... INTO without ORDER BY, which may select non-deterministic rows.
/// </summary>
public sealed class NoTopWithoutOrderByInSelectIntoRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-top-without-order-by-in-select-into",
        Description: "Detects SELECT TOP ... INTO without ORDER BY, which may select non-deterministic rows.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new NoTopWithoutOrderByInSelectIntoVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class NoTopWithoutOrderByInSelectIntoVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SelectStatement node)
        {
            if (node.Into is not null)
            {
                foreach (var querySpec in EnumerateQuerySpecifications(node.QueryExpression))
                {
                    if (querySpec.TopRowFilter is not null &&
                        querySpec.OrderByClause is null &&
                        !IsTopZero(querySpec.TopRowFilter) &&
                        !IsTopOneHundredPercent(querySpec.TopRowFilter))
                    {
                        AddDiagnostic(
                            fragment: querySpec.TopRowFilter,
                            message: "SELECT TOP ... INTO without ORDER BY may select non-deterministic rows. Add an ORDER BY clause to ensure reproducible results."
                        );
                    }
                }
            }

            base.ExplicitVisit(node);
        }

        private static IEnumerable<QuerySpecification> EnumerateQuerySpecifications(QueryExpression queryExpression)
        {
            switch (queryExpression)
            {
                case QuerySpecification querySpecification:
                    yield return querySpecification;
                    break;
                case BinaryQueryExpression binaryQueryExpression:
                    foreach (var querySpecification in EnumerateQuerySpecifications(binaryQueryExpression.FirstQueryExpression))
                    {
                        yield return querySpecification;
                    }

                    foreach (var querySpecification in EnumerateQuerySpecifications(binaryQueryExpression.SecondQueryExpression))
                    {
                        yield return querySpecification;
                    }
                    break;
                case QueryParenthesisExpression parenthesisExpression:
                    foreach (var querySpecification in EnumerateQuerySpecifications(parenthesisExpression.QueryExpression))
                    {
                        yield return querySpecification;
                    }
                    break;
            }
        }

        private static bool IsTopOneHundredPercent(TopRowFilter topRowFilter)
        {
            return topRowFilter.Percent && TryGetLiteralValue(topRowFilter, out var value) && value == 100m;
        }

        private static bool IsTopZero(TopRowFilter topRowFilter)
        {
            return TryGetLiteralValue(topRowFilter, out var value) && value == 0m;
        }

        private static bool TryGetLiteralValue(TopRowFilter topRowFilter, out decimal value)
        {
            if (GetLiteral(topRowFilter.Expression) is not { } literal)
            {
                value = default;
                return false;
            }

            return decimal.TryParse(literal.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        private static Literal? GetLiteral(ScalarExpression expression) =>
            expression switch
            {
                Literal literal => literal,
                ParenthesisExpression parenthesisExpression => GetLiteral(parenthesisExpression.Expression),
                _ => null
            };
    }
}
