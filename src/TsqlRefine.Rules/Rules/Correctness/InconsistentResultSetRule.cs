using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.ControlFlow;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>Detects procedures whose reachable paths return different result-set sequences.</summary>
public sealed class InconsistentResultSetRule : ControlFlowRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        "inconsistent-result-set",
        "Detects procedures that return different result-set shapes on different execution paths.",
        "Correctness",
        RuleSeverity.Warning,
        false);

    private protected override IEnumerable<ControlFlowIssue> AnalyzeScope(ControlFlowScope scope)
    {
        if (scope.Owner is not ProcedureStatementBody procedure)
        {
            return [];
        }

        var states = new ResultShapeAnalysis().Solve(scope.Graph);
        if (!states.TryGetValue(scope.Graph.Exit, out var exitState) ||
            (!exitState.HasOverflow && exitState.Sequences.Count <= 1))
        {
            return [];
        }

        return [new ControlFlowIssue(
            procedure.ProcedureReference?.Name?.BaseIdentifier is { } procedureName ? procedureName : procedure,
            "This procedure returns different result-set column shapes on different execution paths.")];
    }

    private sealed class ResultShapeAnalysis : ForwardDataFlowAnalysis<ResultShapeState>
    {
        protected override ResultShapeState InitialState() => new([string.Empty], false);

        protected override ResultShapeState Transfer(ResultShapeState input, CfgNode node)
        {
            if (node.Statement is not SelectStatement select || GetShape(select) is not { } shape)
            {
                return input;
            }

            var sequences = input.Sequences
                .Select(sequence => Append(sequence, shape))
                .ToHashSet(StringComparer.Ordinal);
            return new ResultShapeState(sequences, input.HasOverflow);
        }

        protected override ResultShapeState Merge(ResultShapeState left, ResultShapeState right)
        {
            var merged = new HashSet<string>(left.Sequences, StringComparer.Ordinal);
            merged.UnionWith(right.Sequences);
            var overflow = left.HasOverflow || right.HasOverflow || merged.Count > 16;
            if (merged.Count > 16)
            {
                merged = merged.Order(StringComparer.Ordinal).Take(16).ToHashSet(StringComparer.Ordinal);
            }
            return new ResultShapeState(merged, overflow);
        }

        protected override bool StateEquals(ResultShapeState left, ResultShapeState right) =>
            left.HasOverflow == right.HasOverflow && left.Sequences.SetEquals(right.Sequences);

        private static string Append(string sequence, string shape)
        {
            var resultCount = sequence.Length == 0 ? 0 : sequence.Count(character => character == '\u001e') + 1;
            if (resultCount >= 4)
            {
                return "<many-result-sets>";
            }
            return sequence.Length == 0 ? shape : $"{sequence}\u001e{shape}";
        }
    }

    private static string? GetShape(SelectStatement select)
    {
        var query = GetQuerySpecification(select.QueryExpression);
        if (query is null || select.Into is not null || query.SelectElements.OfType<SelectSetVariable>().Any())
        {
            return null;
        }

        return string.Join("\u001f", query.SelectElements.Select(GetColumnName));
    }

    private static QuerySpecification? GetQuerySpecification(QueryExpression? expression) => expression switch
    {
        QuerySpecification query => query,
        BinaryQueryExpression binary => GetQuerySpecification(binary.FirstQueryExpression),
        QueryParenthesisExpression parenthesis => GetQuerySpecification(parenthesis.QueryExpression),
        _ => null
    };

    private static string GetColumnName(SelectElement element) => element switch
    {
        SelectStarExpression => "*",
        SelectScalarExpression scalar when scalar.ColumnName?.Identifier?.Value is { } alias => alias,
        SelectScalarExpression { Expression: ColumnReferenceExpression column } =>
            column.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value ?? "?",
        _ => "?"
    };

    private sealed record ResultShapeState(HashSet<string> Sequences, bool HasOverflow);
}
