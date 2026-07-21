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
        private const string DefaultSchemaName = "dbo";
        private const char KeySeparator = '\u001F';

        private readonly List<CreateTableStatement> _candidateTables = [];
        private readonly HashSet<string> _clusteredIndexTables = new(StringComparer.OrdinalIgnoreCase);

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
                _candidateTables.Add(node);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CreateIndexStatement node)
        {
            if (node.Clustered == true && node.OnName?.BaseIdentifier?.Value is { } tableName)
            {
                _clusteredIndexTables.Add(BuildTableKey(
                    node.OnName.SchemaIdentifier?.Value ?? DefaultSchemaName,
                    tableName));
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(TSqlScript node)
        {
            base.ExplicitVisit(node);

            foreach (var table in _candidateTables)
            {
                var tableName = table.SchemaObjectName.BaseIdentifier.Value;
                var key = BuildTableKey(
                    table.SchemaObjectName.SchemaIdentifier?.Value ?? DefaultSchemaName,
                    tableName);
                if (_clusteredIndexTables.Contains(key))
                {
                    continue;
                }

                AddDiagnostic(
                    ScriptDomHelpers.GetFirstTokenRange(table),
                    "Table is created as a heap (no clustered index); consider adding a clustered index to improve performance and reduce fragmentation.");
            }
        }

        private static string BuildTableKey(string schemaName, string tableName) =>
            string.Concat(schemaName, KeySeparator, tableName);

        private static bool HasClusteredTableConstraint(TableDefinition? definition)
        {
            if (definition?.TableConstraints == null)
            {
                return false;
            }

            foreach (var constraint in definition.TableConstraints)
            {
                if (constraint is UniqueConstraintDefinition uniqueConstraint &&
                    IsClusteredConstraint(uniqueConstraint))
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
                    if (constraint is UniqueConstraintDefinition uniqueConstraint &&
                        IsClusteredConstraint(uniqueConstraint))
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

        private static bool IsClusteredConstraint(UniqueConstraintDefinition constraint)
        {
            // Explicit CLUSTERED always means the table is not a heap.
            if (constraint.Clustered == true)
            {
                return true;
            }

            // PRIMARY KEY defaults to CLUSTERED when NONCLUSTERED is not explicitly specified.
            if (constraint.IsPrimaryKey &&
                constraint.Clustered is not false &&
                !ContainsKeyword(constraint, "NONCLUSTERED"))
            {
                return true;
            }

            return false;
        }

        private static bool ContainsKeyword(TSqlFragment fragment, string keyword)
        {
            if (fragment.ScriptTokenStream == null)
            {
                return false;
            }

            for (var i = fragment.FirstTokenIndex; i <= fragment.LastTokenIndex && i < fragment.ScriptTokenStream.Count; i++)
            {
                if (string.Equals(fragment.ScriptTokenStream[i].Text, keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
