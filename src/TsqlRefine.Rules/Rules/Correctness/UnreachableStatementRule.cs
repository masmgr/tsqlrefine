using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.ControlFlow;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>Detects statements that cannot be reached in the structured control-flow graph.</summary>
public sealed class UnreachableStatementRule : ControlFlowRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        "unreachable-statement",
        "Detects statements that are unreachable after control transfer or in constant-false branches.",
        "Correctness",
        RuleSeverity.Warning,
        false);

    private protected override IEnumerable<ControlFlowIssue> AnalyzeScope(ControlFlowScope scope)
    {
        var reachable = new HashSet<CfgNode>();
        var pending = new Stack<CfgNode>();
        pending.Push(scope.Graph.Entry);
        while (pending.TryPop(out var node))
        {
            if (!reachable.Add(node))
            {
                continue;
            }
            foreach (var edge in node.Successors)
            {
                pending.Push(edge.Target);
            }
        }

        return scope.Graph.Nodes
            .Where(node => node.Statement is not null && !reachable.Contains(node))
            .Select(node => new ControlFlowIssue(node.Statement!, "This statement is unreachable."))
            .ToArray();
    }
}
