using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Prohibit full-text search predicates; use alternative search strategies for better performance
/// </summary>
public sealed class FullTextRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-full-text-search",
        Description: "Prohibit full-text search predicates; use alternative search strategies for better performance",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new FullTextVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class FullTextVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(FullTextPredicate node)
        {
            AddDiagnostic(
                fragment: node,
                message: "Full-text search predicate found (CONTAINS/FREETEXT). Consider using alternative search strategies for better performance.",
                code: "avoid-full-text-search",
                category: "Performance",
                fixable: false
            );

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(FullTextTableReference node)
        {
            AddDiagnostic(
                fragment: node,
                message: "Full-text table function found (CONTAINSTABLE/FREETEXTTABLE). Consider using alternative search strategies for better performance.",
                code: "avoid-full-text-search",
                category: "Performance",
                fixable: false
            );

            base.ExplicitVisit(node);
        }
    }
}
