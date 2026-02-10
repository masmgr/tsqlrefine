using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness.Semantic;

/// <summary>
/// Detects duplicate table aliases in the same scope, which causes ambiguous references.
/// </summary>
public sealed class DuplicateAliasRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/duplicate-alias",
        Description: "Detects duplicate table aliases in the same scope, which causes ambiguous references.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new DuplicateAliasVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DuplicateAliasVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SelectStatement node)
        {
            var querySpec = node.QueryExpression as QuerySpecification;
            if (querySpec?.FromClause == null)
            {
                base.ExplicitVisit(node);
                return;
            }

            // Collect all table references from the FROM clause
            var tableReferences = new List<TableReference>();
            TableReferenceHelpers.CollectTableReferences(querySpec.FromClause.TableReferences, tableReferences);

            // Check for duplicates
            foreach (var duplicate in DuplicateNameAnalysisHelpers.FindDuplicateNames(
                tableReferences,
                TableReferenceHelpers.GetAliasOrTableName))
            {
                AddDiagnostic(
                    fragment: duplicate.Item,
                    message: $"Duplicate table alias '{duplicate.Name}' in same scope. Each alias must be unique within a FROM clause.",
                    code: "semantic/duplicate-alias",
                    category: "Correctness",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
