using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Correctness.Semantic;

/// <summary>
/// Detects JOIN operations where the joined table is not referenced in the ON clause.
/// This typically indicates a missing join condition that may result in incorrect query behavior.
/// </summary>
public sealed class JoinTableNotReferencedInOnRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/join-table-not-referenced-in-on",
        Description: "Detects JOIN operations where the joined table is not referenced in the ON clause.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new JoinTableNotReferencedVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
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

            // Collect all table references from the ON clause
            var referencedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var columnRefCollector = new ColumnReferenceCollector(referencedTables);
            node.SearchCondition.Accept(columnRefCollector);

            // Check if the joined table is referenced
            if (!referencedTables.Contains(joinedTableName))
            {
                AddDiagnostic(
                    fragment: node,
                    message: $"Table '{joinedTableName}' is joined but not referenced in the ON clause. This may indicate a missing join condition.",
                    code: "semantic/join-table-not-referenced-in-on",
                    category: "Correctness",
                    fixable: false
                );
            }
        }
    }

    /// <summary>
    /// Collects all table references from ColumnReferenceExpressions in the ON clause.
    /// </summary>
    private sealed class ColumnReferenceCollector : TSqlFragmentVisitor
    {
        private readonly HashSet<string> _referencedTables;

        public ColumnReferenceCollector(HashSet<string> referencedTables)
        {
            _referencedTables = referencedTables;
        }

        public override void ExplicitVisit(ColumnReferenceExpression node)
        {
            // Extract table qualifier from multi-part identifier
            if (node.MultiPartIdentifier?.Identifiers?.Count > 1)
            {
                // First identifier is the table alias/name (e.g., "t2" in "t2.column")
                var tableQualifier = node.MultiPartIdentifier.Identifiers[0].Value;
                _referencedTables.Add(tableQualifier);
            }

            base.ExplicitVisit(node);
        }

        // Don't descend into subqueries - they have their own scope
        public override void ExplicitVisit(ScalarSubquery node)
        {
            // Stop here - don't traverse into scalar subqueries
        }

        public override void ExplicitVisit(SelectStatement node)
        {
            // Stop here - don't traverse into nested SELECT statements
        }
    }
}
