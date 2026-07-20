using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.ControlFlow;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>Detects local variables read before they are definitely assigned on all paths.</summary>
public sealed class VariableUsedBeforeAssignmentRule : ControlFlowRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        "variable-used-before-assignment",
        "Detects variables read before assignment on every path reaching the use.",
        "Correctness",
        RuleSeverity.Warning,
        false);

    private protected override IEnumerable<ControlFlowIssue> AnalyzeScope(ControlFlowScope scope)
    {
        var declarations = VariableAccessAnalysis.GetDeclarations(scope);
        if (declarations.Count == 0)
        {
            return [];
        }

        var initiallyAssigned = declarations.Values
            .Where(declaration => declaration.IsInitiallyAssigned)
            .Select(declaration => declaration.Name);
        var states = new DefiniteAssignmentAnalysis(initiallyAssigned).Solve(scope.Graph);
        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var issues = new List<ControlFlowIssue>();
        foreach (var node in scope.Graph.Nodes)
        {
            if (node.Statement is null || !states.TryGetValue(node, out var assigned))
            {
                continue;
            }

            var accesses = VariableAccessAnalysis.GetAccesses(node.Statement);
            foreach (var read in accesses.Reads)
            {
                if (declarations.ContainsKey(read.Name) &&
                    !assigned.Contains(read.Name) &&
                    reported.Add(read.Name))
                {
                    issues.Add(new ControlFlowIssue(
                        read,
                        $"Variable '{read.Name}' may be used before it is assigned."));
                }
            }
        }
        return issues;
    }

    private sealed class DefiniteAssignmentAnalysis(IEnumerable<string> initiallyAssigned)
        : ForwardDataFlowAnalysis<HashSet<string>>
    {
        private readonly string[] _initiallyAssigned = initiallyAssigned.ToArray();

        protected override HashSet<string> InitialState() =>
            new(_initiallyAssigned, StringComparer.OrdinalIgnoreCase);

        protected override HashSet<string> Transfer(HashSet<string> input, CfgNode node)
        {
            var output = new HashSet<string>(input, StringComparer.OrdinalIgnoreCase);
            if (node.Statement is not null)
            {
                output.UnionWith(VariableAccessAnalysis.GetAccesses(node.Statement).Writes);
            }
            return output;
        }

        protected override HashSet<string> Merge(HashSet<string> left, HashSet<string> right)
        {
            var intersection = new HashSet<string>(left, StringComparer.OrdinalIgnoreCase);
            intersection.IntersectWith(right);
            return intersection;
        }

        protected override bool StateEquals(HashSet<string> left, HashSet<string> right) =>
            left.SetEquals(right);
    }
}
