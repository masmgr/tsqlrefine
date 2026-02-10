using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Disallows TOP in UPDATE/DELETE; it is frequently non-deterministic and easy to misuse without a carefully designed ordering strategy.
/// </summary>
public sealed class AvoidTopInDmlRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-top-in-dml",
        Description: "Disallows TOP in UPDATE/DELETE; it is frequently non-deterministic and easy to misuse without a carefully designed ordering strategy.",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidTopInDmlVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidTopInDmlVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(UpdateStatement node)
        {
            if (node.UpdateSpecification?.TopRowFilter != null)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "Avoid TOP in UPDATE statements; results can be non-deterministic and lead to unpredictable modifications.",
                    code: "avoid-top-in-dml",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DeleteStatement node)
        {
            if (node.DeleteSpecification?.TopRowFilter != null)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "Avoid TOP in DELETE statements; results can be non-deterministic and lead to unpredictable deletions.",
                    code: "avoid-top-in-dml",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
