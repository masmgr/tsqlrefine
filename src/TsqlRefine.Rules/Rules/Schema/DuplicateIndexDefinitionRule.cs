using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects multiple indexes or unique constraints within a table that have the exact same column composition.
/// </summary>
public sealed class DuplicateIndexDefinitionRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "duplicate-index-definition",
        Description: "Detects multiple indexes or unique constraints within a table that have the exact same column composition.",
        Category: "Schema",
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

        var visitor = new DuplicateIndexDefinitionVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
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

            // Collect all index-like definitions: (signature, label, fragment)
            var indexEntries = new List<(string Signature, string Label, TSqlFragment Fragment)>();

            // Inline index definitions
            if (node.Definition.Indexes != null)
            {
                foreach (var index in node.Definition.Indexes)
                {
                    var sig = BuildSignature(index.Columns);
                    if (sig != null)
                    {
                        var label = index.Name?.Value ?? "(unnamed index)";
                        indexEntries.Add((sig, label, index));
                    }
                }
            }

            // Table-level constraints (PRIMARY KEY, UNIQUE)
            if (node.Definition.TableConstraints != null)
            {
                foreach (var constraint in node.Definition.TableConstraints)
                {
                    if (constraint is UniqueConstraintDefinition uniqueConstraint)
                    {
                        var sig = BuildSignature(uniqueConstraint.Columns);
                        if (sig != null)
                        {
                            var kind = uniqueConstraint.IsPrimaryKey ? "PRIMARY KEY" : "UNIQUE";
                            var name = uniqueConstraint.ConstraintIdentifier?.Value;
                            var label = name != null ? $"{kind} '{name}'" : kind;
                            indexEntries.Add((sig, label, constraint));
                        }
                    }
                }
            }

            // Column-level constraints (PRIMARY KEY, UNIQUE)
            if (node.Definition.ColumnDefinitions != null)
            {
                foreach (var column in node.Definition.ColumnDefinitions)
                {
                    if (column.Constraints == null)
                    {
                        continue;
                    }

                    foreach (var constraint in column.Constraints)
                    {
                        if (constraint is UniqueConstraintDefinition uniqueConstraint)
                        {
                            var sig = BuildSignature(uniqueConstraint.Columns);
                            if (sig == null)
                            {
                                // Column-level constraint with no explicit column list — implies the column itself
                                sig = column.ColumnIdentifier?.Value?.ToUpperInvariant() + ":asc";
                            }

                            var kind = uniqueConstraint.IsPrimaryKey ? "PRIMARY KEY" : "UNIQUE";
                            var name = uniqueConstraint.ConstraintIdentifier?.Value;
                            var label = name != null ? $"{kind} '{name}'" : kind;
                            indexEntries.Add((sig, label, constraint));
                        }
                    }
                }
            }

            // Detect duplicates
            var seen = new Dictionary<string, (string Label, TSqlFragment Fragment)>(StringComparer.Ordinal);

            foreach (var (signature, label, fragment) in indexEntries)
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

            base.ExplicitVisit(node);
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
