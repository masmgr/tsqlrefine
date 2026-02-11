using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects duplicate column names in CREATE TABLE definitions.
/// </summary>
public sealed class DuplicateColumnDefinitionRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "duplicate-column-definition",
        Description: "Detects duplicate column names in CREATE TABLE definitions; duplicate columns always cause a runtime error.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new DuplicateColumnDefinitionVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DuplicateColumnDefinitionVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateTableStatement node)
        {
            if (node.Definition?.ColumnDefinitions != null)
            {
                foreach (var duplicate in DuplicateNameAnalysisHelpers.FindDuplicateNames(
                    node.Definition.ColumnDefinitions,
                    column => column.ColumnIdentifier?.Value))
                {
                    AddDiagnostic(
                        fragment: duplicate.Item,
                        message: $"Column '{duplicate.Name}' is defined more than once in the same CREATE TABLE statement.",
                        code: "duplicate-column-definition",
                        category: "Schema",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
