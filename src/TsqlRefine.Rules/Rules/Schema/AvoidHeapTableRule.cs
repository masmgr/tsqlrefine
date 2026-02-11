using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Warns when tables are created as heaps (no clustered index); heaps can lead to unpredictable performance and maintenance costs.
/// </summary>
public sealed class AvoidHeapTableRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-heap-table",
        Description: "Warns when tables are created as heaps (no clustered index); heaps can lead to unpredictable performance and maintenance costs.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidHeapTableVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidHeapTableVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateTableStatement node)
        {
            // Skip temporary tables (#temp, ##temp)
            if (ScriptDomHelpers.IsTemporaryTableName(node.SchemaObjectName?.BaseIdentifier?.Value))
            {
                base.ExplicitVisit(node);
                return;
            }

            var hasClusteredIndex = HasClusteredTableConstraint(node.Definition)
                || HasClusteredColumnConstraint(node.Definition)
                || HasClusteredIndexDefinition(node.Definition);

            if (!hasClusteredIndex)
            {
                AddDiagnostic(
                    range: ScriptDomHelpers.GetFirstTokenRange(node),
                    message: "Table is created as a heap (no clustered index); consider adding a clustered index to improve performance and reduce fragmentation.",
                    code: "avoid-heap-table",
                    category: "Schema",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        private static bool HasClusteredTableConstraint(TableDefinition? definition)
        {
            if (definition?.TableConstraints == null)
            {
                return false;
            }

            foreach (var constraint in definition.TableConstraints)
            {
                if (constraint is UniqueConstraintDefinition { IsPrimaryKey: true, Clustered: true })
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasClusteredColumnConstraint(TableDefinition? definition)
        {
            if (definition?.ColumnDefinitions == null)
            {
                return false;
            }

            foreach (var column in definition.ColumnDefinitions)
            {
                if (column.Constraints == null)
                {
                    continue;
                }

                foreach (var constraint in column.Constraints)
                {
                    if (constraint is UniqueConstraintDefinition { IsPrimaryKey: true, Clustered: true })
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasClusteredIndexDefinition(TableDefinition? definition)
        {
            if (definition?.Indexes == null)
            {
                return false;
            }

            foreach (var index in definition.Indexes)
            {
                if (index.IndexType?.IndexTypeKind is IndexTypeKind.Clustered or IndexTypeKind.ClusteredColumnStore)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
