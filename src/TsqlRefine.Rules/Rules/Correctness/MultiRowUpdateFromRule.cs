using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Warns on UPDATE...FROM with a JOIN, which can match multiple rows per target row
/// and produce non-deterministic updates. This is the schema-free syntactic counterpart to
/// <c>update-join-cardinality-mismatch</c>.
/// </summary>
public sealed class MultiRowUpdateFromRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "multi-row-update-from",
        Description: "Warns on UPDATE...FROM with a JOIN, which can match multiple rows per target row and produce non-deterministic updates.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) => new MultiRowUpdateFromVisitor(Metadata);

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class MultiRowUpdateFromVisitor(RuleMetadata metadata) : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(UpdateStatement node)
        {
            var updateSpec = node.UpdateSpecification;
            if (updateSpec?.FromClause?.TableReferences is not { Count: > 0 } tableRefs)
            {
                base.ExplicitVisit(node);
                return;
            }

            // Report once on the first JOIN, if any JOIN exists.
            var firstJoin = FindFirstJoin(tableRefs);
            if (firstJoin is not null)
            {
                AddDiagnostic(
                    fragment: GetDiagnosticFragment(firstJoin),
                    message: "UPDATE...FROM with a JOIN may match multiple rows per target row, producing a non-deterministic update; ensure the join is unique per target row.",
                    code: metadata.RuleId,
                    category: metadata.Category,
                    fixable: metadata.Fixable
                );
            }

            base.ExplicitVisit(node);
        }

        private static JoinTableReference? FindFirstJoin(IList<TableReference> tableRefs)
        {
            foreach (var tableRef in tableRefs)
            {
                var firstJoin = FindFirstJoin(tableRef);
                if (firstJoin is not null)
                {
                    return firstJoin;
                }
            }

            return null;
        }

        private static JoinTableReference? FindFirstJoin(TableReference tableRef)
        {
            // Parenthesized JOINs are only wrappers, so unwrap them before choosing
            // the single JOIN node to report. Once a real JOIN is found, stop here
            // intentionally: this rule reports once per UPDATE statement and does
            // not walk nested FirstTableReference JOINs looking for an earlier node.
            if (tableRef is JoinParenthesisTableReference { Join: not null } joinParenthesis)
            {
                return FindFirstJoin(joinParenthesis.Join);
            }

            if (tableRef is not JoinTableReference join)
            {
                return null;
            }

            return join;
        }

        private static TSqlFragment GetDiagnosticFragment(JoinTableReference join) =>
            join is QualifiedJoin { SearchCondition: not null } qualifiedJoin
                ? qualifiedJoin.SearchCondition
                : join.SecondTableReference;

    }
}
