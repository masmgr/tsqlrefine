using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.ControlFlow;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>Detects cursors that may reach a scope exit without DEALLOCATE.</summary>
public sealed class CursorNotDeallocatedOnPathRule : ControlFlowRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        "cursor-not-deallocated-on-path",
        "Detects execution paths where an opened cursor is not deallocated.",
        "Correctness",
        RuleSeverity.Warning,
        false);

    private protected override IEnumerable<ControlFlowIssue> AnalyzeScope(ControlFlowScope scope)
    {
        var states = new CursorAnalysis().Solve(scope.Graph);
        if (!states.TryGetValue(scope.Graph.Exit, out var exitState) || exitState.Opened.Count == 0)
        {
            return [];
        }

        var issues = new List<ControlFlowIssue>();
        foreach (var cursorName in exitState.Opened.Order(StringComparer.OrdinalIgnoreCase))
        {
            var open = scope.Graph.Nodes
                .Where(node => states.ContainsKey(node))
                .Select(node => node.Statement)
                .OfType<OpenCursorStatement>()
                .FirstOrDefault(statement => string.Equals(
                    GetCursorName(statement.Cursor), cursorName, StringComparison.OrdinalIgnoreCase));
            if (open is not null)
            {
                issues.Add(new ControlFlowIssue(
                    open,
                    $"Cursor '{cursorName}' is not deallocated on every execution path."));
            }
        }
        return issues;
    }

    private sealed class CursorAnalysis : ForwardDataFlowAnalysis<CursorState>
    {
        protected override CursorState InitialState() => new([]);

        protected override CursorState Transfer(CursorState input, CfgNode node)
        {
            var cursors = new HashSet<string>(input.Opened, StringComparer.OrdinalIgnoreCase);
            switch (node.Statement)
            {
                case OpenCursorStatement open when GetCursorName(open.Cursor) is { } openedName:
                    cursors.Add(openedName);
                    break;
                case DeallocateCursorStatement deallocate when GetCursorName(deallocate.Cursor) is { } closedName:
                    cursors.Remove(closedName);
                    break;
            }
            return new CursorState(cursors);
        }

        protected override CursorState Merge(CursorState left, CursorState right)
        {
            var merged = new HashSet<string>(left.Opened, StringComparer.OrdinalIgnoreCase);
            merged.UnionWith(right.Opened);
            return new CursorState(merged);
        }

        protected override bool StateEquals(CursorState left, CursorState right) =>
            left.Opened.SetEquals(right.Opened);
    }

    private static string? GetCursorName(CursorId? cursor) => cursor?.Name?.Identifier?.Value;

    private sealed record CursorState(HashSet<string> Opened);
}
