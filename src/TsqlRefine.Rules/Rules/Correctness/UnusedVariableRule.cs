using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.ControlFlow;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>Detects declared variables and parameters that are never read.</summary>
public sealed class UnusedVariableRule : ControlFlowRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        "unused-variable",
        "Detects local variables and routine parameters that are never read.",
        "Correctness",
        RuleSeverity.Information,
        false);

    private protected override IEnumerable<ControlFlowIssue> AnalyzeScope(ControlFlowScope scope)
    {
        var declarations = VariableAccessAnalysis.GetDeclarations(scope);
        if (declarations.Count == 0)
        {
            return [];
        }

        var reads = scope.Graph.Nodes
            .Where(node => node.Statement is not null)
            .SelectMany(node => VariableAccessAnalysis.GetAccesses(node.Statement!).Reads)
            .Select(read => read.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return declarations.Values
            .Where(declaration => !reads.Contains(declaration.Name))
            .Select(declaration => new ControlFlowIssue(
                declaration.Identifier,
                declaration.IsParameter
                    ? $"Parameter '{declaration.Name}' is never read."
                    : $"Variable '{declaration.Name}' is never read."))
            .ToArray();
    }
}
