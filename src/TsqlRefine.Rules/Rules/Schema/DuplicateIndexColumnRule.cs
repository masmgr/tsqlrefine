using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects duplicate columns within a single index, PRIMARY KEY, or UNIQUE constraint definition.
/// </summary>
public sealed class DuplicateIndexColumnRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "duplicate-index-column",
        Description: "Detects duplicate columns within a single index, PRIMARY KEY, or UNIQUE constraint definition.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new DuplicateIndexColumnVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DuplicateIndexColumnVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateTableStatement node)
        {
            if (node.Definition == null)
            {
                base.ExplicitVisit(node);
                return;
            }

            // Check inline index definitions
            if (node.Definition.Indexes != null)
            {
                foreach (var index in node.Definition.Indexes)
                {
                    CheckColumnsForDuplicates(index.Columns, index.Name?.Value ?? "(unnamed index)");
                }
            }

            // Check table-level constraints (PRIMARY KEY, UNIQUE)
            if (node.Definition.TableConstraints != null)
            {
                foreach (var constraint in node.Definition.TableConstraints)
                {
                    if (constraint is UniqueConstraintDefinition uniqueConstraint)
                    {
                        var label = uniqueConstraint.IsPrimaryKey ? "PRIMARY KEY" : "UNIQUE";
                        var name = uniqueConstraint.ConstraintIdentifier?.Value;
                        var display = name != null ? $"{label} constraint '{name}'" : label;
                        CheckColumnsForDuplicates(uniqueConstraint.Columns, display);
                    }
                }
            }

            // Check column-level constraints (PRIMARY KEY, UNIQUE)
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
                        if (constraint is UniqueConstraintDefinition uniqueConstraint && uniqueConstraint.Columns?.Count > 1)
                        {
                            var label = uniqueConstraint.IsPrimaryKey ? "PRIMARY KEY" : "UNIQUE";
                            var name = uniqueConstraint.ConstraintIdentifier?.Value;
                            var display = name != null ? $"{label} constraint '{name}'" : label;
                            CheckColumnsForDuplicates(uniqueConstraint.Columns, display);
                        }
                    }
                }
            }

            base.ExplicitVisit(node);
        }

        private void CheckColumnsForDuplicates(IList<ColumnWithSortOrder>? columns, string indexLabel)
        {
            if (columns == null || columns.Count < 2)
            {
                return;
            }

            foreach (var duplicate in DuplicateNameAnalysisHelpers.FindDuplicateNames(columns, GetColumnName))
            {
                AddDiagnostic(
                    fragment: duplicate.Item,
                    message: $"Column '{duplicate.Name}' is specified more than once in {indexLabel}.",
                    code: "duplicate-index-column",
                    category: "Schema",
                    fixable: false
                );
            }
        }

        private static string? GetColumnName(ColumnWithSortOrder col)
        {
            return col.Column?.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value;
        }
    }
}
