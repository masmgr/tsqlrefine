using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.ControlFlow;

/// <summary>Base class for rules that analyze each supported executable CFG scope.</summary>
public abstract class ControlFlowRuleBase : IRule
{
    public abstract RuleMetadata Metadata { get; }

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Ast.Fragment is null)
        {
            return [];
        }

        return ControlFlowScopeCollector.Collect(context.Ast.Fragment)
            .Where(scope => scope.IsSupported)
            .SelectMany(AnalyzeScope)
            .Select(issue => new Diagnostic(
                ScriptDomHelpers.GetRange(issue.Fragment),
                issue.Message,
                Code: Metadata.RuleId,
                Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, false)))
            .ToArray();
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private protected abstract IEnumerable<ControlFlowIssue> AnalyzeScope(ControlFlowScope scope);
}

internal sealed record ControlFlowIssue(TSqlFragment Fragment, string Message);
