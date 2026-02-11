using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects duplicate column names in INSERT column lists.
/// </summary>
public sealed class DuplicateInsertColumnRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "duplicate-insert-column",
        Description: "Detects duplicate column names in INSERT column lists; duplicate columns always cause a runtime error.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new DuplicateInsertColumnVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DuplicateInsertColumnVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(InsertStatement node)
        {
            var columns = node.InsertSpecification?.Columns;
            if (columns is not null && columns.Count >= 2)
            {
                foreach (var duplicate in DuplicateNameAnalysisHelpers.FindDuplicateNames(columns, GetColumnName))
                {
                    AddDiagnostic(
                        fragment: duplicate.Item,
                        message: $"Column '{duplicate.Name}' is specified more than once in the INSERT column list.",
                        code: "duplicate-insert-column",
                        category: "Correctness",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }

        private static string? GetColumnName(ColumnReferenceExpression column)
        {
            var identifiers = column.MultiPartIdentifier?.Identifiers;
            if (identifiers is null || identifiers.Count == 0)
            {
                return null;
            }

            return identifiers[^1].Value;
        }
    }
}
