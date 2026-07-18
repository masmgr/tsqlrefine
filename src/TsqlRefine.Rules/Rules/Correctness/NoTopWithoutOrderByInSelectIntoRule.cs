using System.Globalization;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects SELECT TOP ... INTO without ORDER BY, which creates permanent tables with non-deterministic data.
/// </summary>
public sealed class NoTopWithoutOrderByInSelectIntoRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-top-without-order-by-in-select-into",
        Description: "Detects SELECT TOP ... INTO without ORDER BY, which creates permanent tables with non-deterministic data.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Error,
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
                        !IsTopOneHundredPercent(querySpec.TopRowFilter))
                    {
                        AddDiagnostic(
                            fragment: querySpec.TopRowFilter,
                            message: "SELECT TOP ... INTO without ORDER BY creates a permanent table with non-deterministic data. Add an ORDER BY clause to ensure reproducible results.",
                            code: "avoid-top-without-order-by-in-select-into",
                            category: "Correctness",
                            fixable: false
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
            if (!topRowFilter.Percent || GetLiteral(topRowFilter.Expression) is not { } literal)
            {
                return false;
            }

            return decimal.TryParse(literal.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) &&
                value == 100m;
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
