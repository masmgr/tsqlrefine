using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Safety;

/// <summary>
/// Requires IF EXISTS on DROP statements for idempotent deployment scripts.
/// </summary>
public sealed class RequireDropIfExistsRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "require-drop-if-exists",
        Description: "Requires IF EXISTS on DROP statements for idempotent deployment scripts.",
        Category: "Safety",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new RequireDropIfExistsVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireDropIfExistsVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(DropTableStatement node)
        {
            if (!node.IsIfExists)
            {
                // Skip temp tables
                if (node.Objects is { Count: > 0 } &&
                    node.Objects.All(o => ScriptDomHelpers.IsTemporaryTableName(o.BaseIdentifier?.Value)))
                {
                    base.ExplicitVisit(node);
                    return;
                }

                AddDropDiagnostic(node, "TABLE");
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DropProcedureStatement node)
        {
            if (!node.IsIfExists)
            {
                AddDropDiagnostic(node, "PROCEDURE");
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DropViewStatement node)
        {
            if (!node.IsIfExists)
            {
                AddDropDiagnostic(node, "VIEW");
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DropFunctionStatement node)
        {
            if (!node.IsIfExists)
            {
                AddDropDiagnostic(node, "FUNCTION");
            }

            base.ExplicitVisit(node);
        }

        private void AddDropDiagnostic(TSqlFragment node, string objectType)
        {
            AddDiagnostic(
                range: ScriptDomHelpers.GetLeadingKeywordPairRange(node),
                message: $"DROP {objectType} should use IF EXISTS for idempotent deployment scripts. Use 'DROP {objectType} IF EXISTS' to avoid errors when the object does not exist.",
                code: "require-drop-if-exists",
                category: "Safety",
                fixable: false
            );
        }
    }
}
