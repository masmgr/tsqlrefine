using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Detects OR conditions on different columns in predicates which may prevent index usage and cause table scans.
/// </summary>
public sealed class AvoidOrOnDifferentColumnsRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-or-on-different-columns",
        Description: "Detects OR conditions on different columns in predicates which may prevent index usage and cause table scans.",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidOrOnDifferentColumnsVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidOrOnDifferentColumnsVisitor : PredicateAwareVisitorBase
    {
        public override void ExplicitVisit(BooleanBinaryExpression node)
        {
            if (IsInPredicate && node.BinaryExpressionType == BooleanBinaryExpressionType.Or)
            {
                var leftComparison = UnwrapToComparison(node.FirstExpression);
                var rightComparison = UnwrapToComparison(node.SecondExpression);

                if (leftComparison is not null && rightComparison is not null)
                {
                    var leftColumns = GetColumnBaseNames(leftComparison);
                    var rightColumns = GetColumnBaseNames(rightComparison);

                    if (leftColumns.Count > 0 && rightColumns.Count > 0 && !leftColumns.Overlaps(rightColumns))
                    {
                        AddDiagnostic(
                            fragment: node,
                            message: "OR condition spans different columns, which may prevent efficient index usage. Consider rewriting as UNION ALL of separate queries, or ensure appropriate indexing covers both columns.",
                            code: "avoid-or-on-different-columns",
                            category: "Performance",
                            fixable: false
                        );
                    }
                }
            }

            base.ExplicitVisit(node);
        }

        private static BooleanComparisonExpression? UnwrapToComparison(BooleanExpression expr)
        {
            while (expr is BooleanParenthesisExpression paren)
            {
                expr = paren.Expression;
            }

            return expr as BooleanComparisonExpression;
        }

        private static HashSet<string> GetColumnBaseNames(BooleanComparisonExpression comparison)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddColumnBaseName(comparison.FirstExpression, names);
            AddColumnBaseName(comparison.SecondExpression, names);
            return names;
        }

        private static void AddColumnBaseName(ScalarExpression? expr, HashSet<string> names)
        {
            if (expr is ColumnReferenceExpression colRef)
            {
                var identifiers = colRef.MultiPartIdentifier?.Identifiers;
                if (identifiers is { Count: > 0 })
                {
                    names.Add(identifiers[^1].Value);
                }
            }
        }
    }
}
