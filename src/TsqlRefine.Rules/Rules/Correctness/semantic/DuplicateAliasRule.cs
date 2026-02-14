using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness.Semantic;

/// <summary>
/// Detects duplicate table aliases in the same scope, which causes ambiguous references.
/// </summary>
public sealed class DuplicateAliasRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "semantic-duplicate-alias",
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
        public override void ExplicitVisit(QuerySpecification node)
        {
            ReportDuplicateAliases(node.FromClause?.TableReferences);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(UpdateSpecification node)
        {
            ReportDuplicateAliases(node.FromClause?.TableReferences);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DeleteSpecification node)
        {
            ReportDuplicateAliases(node.FromClause?.TableReferences);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(MergeSpecification node)
        {
            ReportDuplicateAliasesInMergeScope(node);
            base.ExplicitVisit(node);
        }

        private void ReportDuplicateAliases(IList<TableReference>? tableReferences)
        {
            if (tableReferences == null || tableReferences.Count == 0)
            {
                return;
            }

            var flattenedReferences = new List<TableReference>();
            TableReferenceHelpers.CollectTableReferences(tableReferences, flattenedReferences);

            foreach (var duplicate in DuplicateNameAnalysisHelpers.FindDuplicateNames(
                flattenedReferences,
                TableReferenceHelpers.GetAliasOrTableName))
            {
                AddDuplicateAliasDiagnostic(duplicate.Item, duplicate.Name);
            }
        }

        private void ReportDuplicateAliasesInMergeScope(MergeSpecification mergeSpecification)
        {
            var scopeEntries = new List<(TSqlFragment Fragment, string? Name)>();
            var targetName = GetMergeTargetAliasOrName(mergeSpecification);
            var targetFragment = (TSqlFragment?)mergeSpecification.TableAlias ?? mergeSpecification.Target;

            if (targetFragment != null)
            {
                scopeEntries.Add((targetFragment, targetName));
            }

            if (mergeSpecification.TableReference != null)
            {
                var sourceReferences = new List<TableReference>();
                TableReferenceHelpers.CollectTableReferences([mergeSpecification.TableReference], sourceReferences);

                foreach (var sourceReference in sourceReferences)
                {
                    scopeEntries.Add((sourceReference, TableReferenceHelpers.GetAliasOrTableName(sourceReference)));
                }
            }

            foreach (var duplicate in DuplicateNameAnalysisHelpers.FindDuplicateNames(
                scopeEntries,
                entry => entry.Name))
            {
                AddDuplicateAliasDiagnostic(duplicate.Item.Fragment, duplicate.Name);
            }
        }

        private static string? GetMergeTargetAliasOrName(MergeSpecification mergeSpecification)
        {
            if (!string.IsNullOrWhiteSpace(mergeSpecification.TableAlias?.Value))
            {
                return mergeSpecification.TableAlias.Value;
            }

            return mergeSpecification.Target == null
                ? null
                : TableReferenceHelpers.GetAliasOrTableName(mergeSpecification.Target);
        }

        private void AddDuplicateAliasDiagnostic(TSqlFragment fragment, string aliasName)
        {
            AddDiagnostic(
                fragment: fragment,
                message: $"Duplicate table alias '{aliasName}' in same scope. Each alias must be unique within a FROM clause.",
                code: "semantic-duplicate-alias",
                category: "Correctness",
                fixable: false
            );
        }
    }
}
