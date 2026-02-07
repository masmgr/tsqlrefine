using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Diagnostics;
using TsqlRefine.Rules.Helpers.Visitors;

namespace TsqlRefine.Rules.Rules.Security;

public sealed class AvoidOpenrowsetOpendatasourceRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-openrowset-opendatasource",
        Description: "Detects OPENROWSET and OPENDATASOURCE usage, which can be exploited for unauthorized remote data access.",
        Category: "Security",
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

        var visitor = new OpenrowsetOpendatasourceVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class OpenrowsetOpendatasourceVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(OpenRowsetTableReference node)
        {
            AddDiagnostic(
                fragment: node,
                message: "Avoid OPENROWSET for remote data access. It can expand the attack surface for SQL injection and enable unauthorized data exfiltration. Consider using linked servers with proper security configuration or ETL processes.",
                code: "avoid-openrowset-opendatasource",
                category: "Security",
                fixable: false
            );

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(BulkOpenRowset node)
        {
            AddDiagnostic(
                fragment: node,
                message: "Avoid OPENROWSET(BULK ...) for file-based data access. It can enable unauthorized file access and data exfiltration. Consider using BULK INSERT with proper permissions or ETL processes.",
                code: "avoid-openrowset-opendatasource",
                category: "Security",
                fixable: false
            );

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(AdHocTableReference node)
        {
            AddDiagnostic(
                fragment: node,
                message: "Avoid OPENDATASOURCE for ad-hoc remote connections. It can expand the attack surface for SQL injection and enable unauthorized data exfiltration. Consider using linked servers with proper security configuration or ETL processes.",
                code: "avoid-openrowset-opendatasource",
                category: "Security",
                fixable: false
            );

            base.ExplicitVisit(node);
        }
    }
}
