using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness.Semantic;

/// <summary>
/// Detects JOIN conditions that are always true or likely incorrect, such as 'ON 1=1' or self-comparisons.
/// </summary>
public sealed class JoinConditionAlwaysTrueRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "semantic-join-condition-always-true",
        Description: "Detects JOIN conditions that are always true or likely incorrect, such as 'ON 1=1' or self-comparisons.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new JoinConditionAlwaysTrueVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class JoinConditionAlwaysTrueVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(QualifiedJoin node)
        {
            if (node.SearchCondition != null)
            {
                CheckJoinCondition(node, node.SearchCondition);
            }

            base.ExplicitVisit(node);
        }

        private void CheckJoinCondition(QualifiedJoin join, BooleanExpression condition)
        {
            if (condition is BooleanComparisonExpression comparison)
            {
                // Check for literal comparisons like 1=1, 0=0, 'a'='a'
                if (AreLiteralsEqual(comparison.FirstExpression, comparison.SecondExpression))
                {
                    if (CanReturnAtMostOneRow(join.SecondTableReference))
                    {
                        return;
                    }

                    AddDiagnostic(
                        comparison,
                        message: "JOIN uses an always-true literal condition (e.g., '1=1'). Verify that the Cartesian semantics are intentional.",
                        severity: DiagnosticSeverity.Information
                    );
                    return;
                }

                // Check for self-comparisons like t1.col = t1.col using helper
                if (comparison.FirstExpression is ColumnReferenceExpression firstCol &&
                    comparison.SecondExpression is ColumnReferenceExpression secondCol &&
                    ColumnReferenceHelpers.AreColumnReferencesEqual(firstCol, secondCol))
                {
                    AddDiagnostic(
                        comparison,
                        message: "JOIN condition compares a column to itself (e.g., 't1.col = t1.col'). This is always true and likely incorrect.",
                        severity: DiagnosticSeverity.Warning
                    );
                    return;
                }
            }
            else if (condition is BooleanBinaryExpression binaryExpr)
            {
                // Recursively check AND/OR expressions
                CheckJoinCondition(join, binaryExpr.FirstExpression);
                CheckJoinCondition(join, binaryExpr.SecondExpression);
            }
        }

        private static bool AreLiteralsEqual(ScalarExpression? first, ScalarExpression? second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            // Check integer literals
            if (first is IntegerLiteral firstInt && second is IntegerLiteral secondInt)
            {
                return firstInt.Value == secondInt.Value;
            }

            // Check numeric literals
            if (first is NumericLiteral firstNum && second is NumericLiteral secondNum)
            {
                return firstNum.Value == secondNum.Value;
            }

            // Check string literals
            if (first is StringLiteral firstStr && second is StringLiteral secondStr)
            {
                return firstStr.Value == secondStr.Value;
            }

            return false;
        }

        private static bool CanReturnAtMostOneRow(TableReference tableReference)
        {
            if (tableReference is not QueryDerivedTable { QueryExpression: QuerySpecification query })
            {
                return false;
            }

            if (query.TopRowFilter is { Percent: false } top && IsAtMostOne(top.Expression))
            {
                return true;
            }

            if (query.GroupByClause is not null)
            {
                return false;
            }

            var aggregateCollector = new AggregateFunctionCollector();
            foreach (var element in query.SelectElements.OfType<SelectScalarExpression>())
            {
                element.Expression.Accept(aggregateCollector);
            }

            return aggregateCollector.HasAggregate;
        }

        private static bool IsAtMostOne(ScalarExpression expression)
        {
            expression = expression is ParenthesisExpression parenthesis
                ? parenthesis.Expression
                : expression;
            return expression is IntegerLiteral literal &&
                   int.TryParse(literal.Value, out var value) &&
                   value <= 1;
        }

        private sealed class AggregateFunctionCollector : TSqlFragmentVisitor
        {
            public bool HasAggregate { get; private set; }

            public override void ExplicitVisit(FunctionCall node)
            {
                if (node.OverClause is null && AggregateFunctionHelpers.IsAggregateFunction(node))
                {
                    HasAggregate = true;
                }
            }

            public override void ExplicitVisit(ScalarSubquery node)
            {
                // An aggregate in a nested scalar subquery does not constrain the derived table.
            }
        }
    }
}
