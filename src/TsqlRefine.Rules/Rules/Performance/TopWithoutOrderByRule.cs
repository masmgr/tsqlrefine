using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Detects TOP clause without ORDER BY, which produces non-deterministic results.
/// When schema information is available, suppresses the diagnostic if the WHERE clause
/// filters on a unique column set (PK, unique constraint, or unique index), guaranteeing
/// at most one row regardless of ordering.
/// </summary>
public sealed class TopWithoutOrderByRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "top-without-order-by",
        Description: "Detects TOP clause without ORDER BY, which produces non-deterministic results.",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new TopWithoutOrderByVisitor(context.Schema);

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506", Justification = "The visitor combines existing TOP, query-shape, and schema analyses; this rule-specific coupling is intentional.")]
    private sealed class TopWithoutOrderByVisitor(ISchemaProvider? schema) : DiagnosticVisitorBase
    {
        private readonly HashSet<QuerySpecification> _selectIntoQueries = [];
        private int _existsDepth;

        public override void ExplicitVisit(ExistsPredicate node)
        {
            _existsDepth++;
            base.ExplicitVisit(node);
            _existsDepth--;
        }

        public override void ExplicitVisit(SelectStatement node)
        {
            if (node.Into is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var added = QueryExpressionAnalysisHelpers.EnumerateQuerySpecifications(node.QueryExpression)
                .Where(_selectIntoQueries.Add)
                .ToArray();
            base.ExplicitVisit(node);
            foreach (var query in added)
            {
                _selectIntoQueries.Remove(query);
            }
        }

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (_existsDepth == 0 &&
                !_selectIntoQueries.Contains(node) &&
                node.TopRowFilter != null &&
                node.OrderByClause == null &&
                !IsTopZero(node.TopRowFilter.Expression) &&
                !IsTopOneHundredPercent(node.TopRowFilter) &&
                !IsSingleRowAggregateQuery(node))
            {
                if (!QueryCardinalityAnalysisHelpers.ReturnsAtMostOneRow(node, schema))
                {
                    AddDiagnostic(
                        fragment: node.TopRowFilter,
                        message: "TOP clause without ORDER BY produces non-deterministic results. Add an ORDER BY clause to ensure consistent results.",
                        code: "top-without-order-by",
                        category: "Performance",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }

        private static bool IsTopZero(ScalarExpression expression) =>
            expression is IntegerLiteral lit && lit.Value == "0" ||
            expression is ParenthesisExpression paren && IsTopZero(paren.Expression);

        private static bool IsTopOneHundredPercent(TopRowFilter topRowFilter) =>
            topRowFilter.Percent &&
            TryGetLiteralValue(topRowFilter.Expression, out var value) &&
            value == 100m;

        private static bool TryGetLiteralValue(ScalarExpression expression, out decimal value)
        {
            if (expression is ParenthesisExpression parenthesis)
            {
                return TryGetLiteralValue(parenthesis.Expression, out value);
            }

            if (expression is not Literal literal)
            {
                value = default;
                return false;
            }

            return decimal.TryParse(literal.Value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        private static bool IsSingleRowAggregateQuery(QuerySpecification node)
        {
            if (node.GroupByClause is not null || node.SelectElements.Count == 0)
            {
                return false;
            }

            return node.SelectElements.All(selectElement => selectElement switch
            {
                SelectScalarExpression { Expression: FunctionCall function } => IsNonWindowAggregate(function),
                SelectSetVariable { Expression: FunctionCall function } => IsNonWindowAggregate(function),
                _ => false
            });
        }

        private static bool IsNonWindowAggregate(FunctionCall function) =>
            function.CallTarget is null &&
            function.OverClause is null &&
            TsqlRefine.Rules.Helpers.Analysis.AggregateFunctionHelpers.IsAggregateFunction(function);

    }
}
