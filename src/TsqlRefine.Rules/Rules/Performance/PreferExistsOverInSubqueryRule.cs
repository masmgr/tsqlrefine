using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Detects WHERE column IN (SELECT ...) patterns and recommends using EXISTS instead for better performance with large datasets.
/// </summary>
public sealed class PreferExistsOverInSubqueryRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "prefer-exists-over-in-subquery",
        Description: "Detects WHERE column IN (SELECT ...) patterns and recommends using EXISTS instead for better performance with large datasets.",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new PreferExistsOverInSubqueryVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class PreferExistsOverInSubqueryVisitor : PredicateAwareVisitorBase
    {
        public override void ExplicitVisit(InPredicate node)
        {
            // Only flag IN with subquery, not IN with value lists
            if (IsInPredicate && node.Subquery is not null && !HasIsNotNullOnSelectColumn(node.Subquery))
            {
                AddDiagnostic(
                    range: ScriptDomHelpers.GetInKeywordRange(node),
                    message: "Consider using EXISTS instead of IN with a subquery. EXISTS can be more efficient for large datasets as it short-circuits once a match is found.",
                    code: "prefer-exists-over-in-subquery",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        /// <summary>
        /// Checks whether the subquery has an IS NOT NULL check on the same column
        /// as its single SELECT column. When present, the developer is intentionally
        /// guarding against NULLs and the IN pattern should not be flagged.
        /// </summary>
        private static bool HasIsNotNullOnSelectColumn(ScalarSubquery subquery)
        {
            if (subquery.QueryExpression is not QuerySpecification querySpec)
                return false;

            if (querySpec.SelectElements is not { Count: 1 })
                return false;

            if (querySpec.SelectElements[0] is not SelectScalarExpression { Expression: ColumnReferenceExpression selectColRef })
                return false;

            if (querySpec.WhereClause?.SearchCondition is null)
                return false;

            return ContainsIsNotNullForColumn(querySpec.WhereClause.SearchCondition, selectColRef);
        }

        private static bool ContainsIsNotNullForColumn(BooleanExpression condition, ColumnReferenceExpression selectedColumn)
        {
            switch (condition)
            {
                case BooleanIsNullExpression { IsNot: true } isNotNull:
                    return isNotNull.Expression is ColumnReferenceExpression colRef
                        && AreSameColumnReference(colRef, selectedColumn);

                case BooleanBinaryExpression binary:
                    if (binary.BinaryExpressionType == BooleanBinaryExpressionType.And)
                    {
                        return ContainsIsNotNullForColumn(binary.FirstExpression, selectedColumn)
                            || ContainsIsNotNullForColumn(binary.SecondExpression, selectedColumn);
                    }

                    // OR: both branches must contain IS NOT NULL to guarantee filtering
                    return ContainsIsNotNullForColumn(binary.FirstExpression, selectedColumn)
                        && ContainsIsNotNullForColumn(binary.SecondExpression, selectedColumn);

                case BooleanParenthesisExpression paren:
                    return ContainsIsNotNullForColumn(paren.Expression, selectedColumn);

                default:
                    return false;
            }
        }

        private static bool AreSameColumnReference(ColumnReferenceExpression left, ColumnReferenceExpression right)
        {
            var leftIds = left.MultiPartIdentifier?.Identifiers;
            var rightIds = right.MultiPartIdentifier?.Identifiers;

            if (leftIds is null || rightIds is null)
            {
                return false;
            }

            if (leftIds.Count != rightIds.Count)
            {
                return false;
            }

            for (var i = 0; i < leftIds.Count; i++)
            {
                if (!string.Equals(leftIds[i].Value, rightIds[i].Value, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
