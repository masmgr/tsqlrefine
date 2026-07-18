using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects columns in HAVING clause that are neither in the GROUP BY clause nor wrapped in an aggregate function.
/// SQL Server raises error 8120 for such columns at runtime.
/// </summary>
public sealed class HavingColumnMismatchRule : DiagnosticVisitorRuleBase
{
    private const string RuleId = "having-column-mismatch";
    private const string Category = "Correctness";

    public override RuleMetadata Metadata { get; } = new(
        RuleId: RuleId,
        Description: "Detects columns in HAVING clause not in GROUP BY and not wrapped in an aggregate function.",
        Category: Category,
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false);

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new HavingColumnMismatchVisitor();

    private sealed class HavingColumnMismatchVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.HavingClause?.SearchCondition is not null &&
                node.GroupByClause?.GroupingSpecifications is { Count: > 0 })
            {
                var groupByExpressions = GroupByColumnAnalysisHelpers.CollectGroupByExpressions(node.GroupByClause);
                var columnReferences = new List<ColumnReferenceExpression>();
                GroupByColumnAnalysisHelpers.CollectColumnReferencesFromBooleanExpression(
                    node.HavingClause.SearchCondition,
                    columnReferences,
                    groupByExpressions);

                foreach (var columnReference in GroupByColumnAnalysisHelpers.FilterUngroupedColumns(columnReferences, groupByExpressions))
                {
                    var columnName = GroupByColumnAnalysisHelpers.GetColumnDisplayName(columnReference);
                    AddDiagnostic(
                        fragment: columnReference,
                        message: $"Column '{columnName}' in HAVING clause is not contained in GROUP BY or an aggregate function.",
                        code: RuleId,
                        category: Category,
                        fixable: false);
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
