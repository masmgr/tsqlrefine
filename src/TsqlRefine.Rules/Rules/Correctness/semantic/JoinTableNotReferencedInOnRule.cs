using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness.Semantic;

/// <summary>
/// Detects JOIN operations where the joined table is not referenced in the ON clause.
/// This typically indicates a missing join condition that may result in incorrect query behavior.
/// </summary>
public sealed class JoinTableNotReferencedInOnRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "semantic-join-table-not-referenced-in-on",
        Description: "Detects JOIN operations where the joined table is not referenced in the ON clause.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new JoinTableNotReferencedVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class JoinTableNotReferencedVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(QualifiedJoin node)
        {
            // Only check INNER, LEFT OUTER, RIGHT OUTER joins
            if (node.QualifiedJoinType is QualifiedJoinType.Inner
                or QualifiedJoinType.LeftOuter
                or QualifiedJoinType.RightOuter)
            {
                CheckJoinTableReference(node);
            }

            // Continue traversing for nested JOINs
            base.ExplicitVisit(node);
        }

        private void CheckJoinTableReference(QualifiedJoin node)
        {
            // Get the alias or table name of the joined table (right side)
            var joinedTableName = TableReferenceHelpers.GetAliasOrTableName(node.SecondTableReference);

            // If we couldn't determine the table name, skip (e.g., complex table expressions)
            if (joinedTableName == null)
            {
                return;
            }

            // If there's no ON clause, skip (shouldn't happen for QualifiedJoin but defensive)
            if (node.SearchCondition == null)
            {
                return;
            }

            // Collect all table references from the ON clause using helper
            var referencedTables = ColumnReferenceHelpers.CollectTableQualifiers(node.SearchCondition);

            // Check if the joined table is referenced
            if (!referencedTables.Contains(joinedTableName))
            {
                AddDiagnostic(
                    fragment: node,
                    message: $"Table '{joinedTableName}' is joined but not referenced in the ON clause. This may indicate a missing join condition.",
                    code: "semantic-join-table-not-referenced-in-on",
                    category: "Correctness",
                    fixable: false
                );
            }
        }
    }
}
