using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects SELECT columns that are neither in the GROUP BY clause nor wrapped in an aggregate function.
/// SQL Server raises an error for such columns at runtime.
/// </summary>
public sealed class GroupByColumnMismatchRule : DiagnosticVisitorRuleBase
{
    private const string RuleId = "group-by-column-mismatch";
    private const string Category = "Correctness";

    public override RuleMetadata Metadata { get; } = new(
        RuleId: RuleId,
        Description: "Detects SELECT columns not contained in GROUP BY or an aggregate function.",
        Category: Category,
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false);

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new GroupByColumnMismatchVisitor();

    private sealed class GroupByColumnMismatchVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.GroupByClause?.GroupingSpecifications is { Count: > 0 })
            {
                var groupByExpressions = GroupByColumnAnalysisHelpers.CollectGroupByExpressions(node.GroupByClause);
                foreach (var element in node.SelectElements.OfType<SelectScalarExpression>())
                {
                    CheckExpression(element.Expression, groupByExpressions);
                }
            }

            base.ExplicitVisit(node);
        }

        private void CheckExpression(ScalarExpression expression, List<ScalarExpression> groupByExpressions)
        {
            if (expression is FunctionCall function && GroupByColumnAnalysisHelpers.IsAggregateFunction(function) ||
                GroupByColumnAnalysisHelpers.IsExpressionInGroupBy(expression, groupByExpressions))
            {
                return;
            }

            var columnReferences = new List<ColumnReferenceExpression>();
            GroupByColumnAnalysisHelpers.CollectColumnReferences(expression, columnReferences, groupByExpressions);
            foreach (var columnReference in GroupByColumnAnalysisHelpers.FilterUngroupedColumns(columnReferences, groupByExpressions))
            {
                var columnName = GroupByColumnAnalysisHelpers.GetColumnDisplayName(columnReference);
                AddDiagnostic(
                    fragment: columnReference,
                    message: $"Column '{columnName}' is not contained in GROUP BY or an aggregate function.",
                    code: RuleId,
                    category: Category,
                    fixable: false);
            }
        }
    }
}
