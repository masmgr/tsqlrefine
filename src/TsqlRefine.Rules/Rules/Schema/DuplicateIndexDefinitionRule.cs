using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects multiple indexes or unique constraints within a table that have the exact same column composition.
/// </summary>
public sealed class DuplicateIndexDefinitionRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "duplicate-index-definition",
        Description: "Detects multiple indexes or unique constraints within a table that have the exact same column composition.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new DuplicateIndexDefinitionVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DuplicateIndexDefinitionVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateTableStatement node)
        {
            if (node.Definition == null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var indexEntries = new List<(string Signature, string Label, TSqlFragment Fragment)>();
            CollectIndexDefinitions(node.Definition, indexEntries);
            CollectTableConstraints(node.Definition, indexEntries);
            CollectColumnConstraints(node.Definition, indexEntries);
            ReportDuplicates(indexEntries);

            base.ExplicitVisit(node);
        }

        private static void CollectIndexDefinitions(
            TableDefinition definition,
            List<(string Signature, string Label, TSqlFragment Fragment)> entries)
        {
            if (definition.Indexes == null)
            {
                return;
            }

            foreach (var index in definition.Indexes)
            {
                var sig = BuildSignature(index.Columns);
                if (sig != null)
                {
                    var label = index.Name?.Value ?? "(unnamed index)";
                    entries.Add((sig, label, index));
                }
            }
        }

        private static void CollectTableConstraints(
            TableDefinition definition,
            List<(string Signature, string Label, TSqlFragment Fragment)> entries)
        {
            if (definition.TableConstraints == null)
            {
                return;
            }

            foreach (var constraint in definition.TableConstraints)
            {
                if (constraint is UniqueConstraintDefinition uniqueConstraint)
                {
                    var sig = BuildSignature(uniqueConstraint.Columns);
                    if (sig != null)
                    {
                        var label = FormatConstraintLabel(uniqueConstraint);
                        entries.Add((sig, label, constraint));
                    }
                }
            }
        }

        private static void CollectColumnConstraints(
            TableDefinition definition,
            List<(string Signature, string Label, TSqlFragment Fragment)> entries)
        {
            if (definition.ColumnDefinitions == null)
            {
                return;
            }

            foreach (var column in definition.ColumnDefinitions)
            {
                if (column.Constraints == null)
                {
                    continue;
                }

                foreach (var constraint in column.Constraints)
                {
                    if (constraint is UniqueConstraintDefinition uniqueConstraint)
                    {
                        // Column-level constraint with no explicit column list — implies the column itself
                        var sig = BuildSignature(uniqueConstraint.Columns)
                            ?? column.ColumnIdentifier?.Value?.ToUpperInvariant() + ":asc";

                        var label = FormatConstraintLabel(uniqueConstraint);
                        entries.Add((sig!, label, constraint));
                    }
                }
            }
        }

        private void ReportDuplicates(List<(string Signature, string Label, TSqlFragment Fragment)> entries)
        {
            var seen = new Dictionary<string, (string Label, TSqlFragment Fragment)>(StringComparer.Ordinal);

            foreach (var (signature, label, fragment) in entries)
            {
                if (seen.TryGetValue(signature, out var first))
                {
                    AddDiagnostic(
                        fragment: fragment,
                        message: $"{label} has the same column composition as {first.Label}.",
                        code: "duplicate-index-definition",
                        category: "Schema",
                        fixable: false
                    );
                }
                else
                {
                    seen[signature] = (label, fragment);
                }
            }
        }

        private static string FormatConstraintLabel(UniqueConstraintDefinition constraint)
        {
            var kind = constraint.IsPrimaryKey ? "PRIMARY KEY" : "UNIQUE";
            var name = constraint.ConstraintIdentifier?.Value;
            return name != null ? $"{kind} '{name}'" : kind;
        }

        private static string? BuildSignature(IList<ColumnWithSortOrder>? columns)
        {
            if (columns == null || columns.Count == 0)
            {
                return null;
            }

            var parts = new string[columns.Count];
            for (var i = 0; i < columns.Count; i++)
            {
                var colName = columns[i].Column?.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value;
                if (colName == null)
                {
                    return null;
                }

                var sort = columns[i].SortOrder == SortOrder.Descending ? "desc" : "asc";
                parts[i] = colName.ToUpperInvariant() + ":" + sort;
            }

            return string.Join(",", parts);
        }
    }
}
