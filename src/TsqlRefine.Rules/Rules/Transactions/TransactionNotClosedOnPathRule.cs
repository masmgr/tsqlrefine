using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.ControlFlow;

namespace TsqlRefine.Rules.Rules.Transactions;

/// <summary>Detects locally opened transactions that remain open on a reachable exit path.</summary>
public sealed class TransactionNotClosedOnPathRule : ControlFlowRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        "transaction-not-closed-on-path",
        "Detects execution paths that leave a transaction opened by the current scope unclosed.",
        "Transactions",
        RuleSeverity.Error,
        false);

    private protected override IEnumerable<ControlFlowIssue> AnalyzeScope(ControlFlowScope scope)
    {
        var states = new TransactionAnalysis().Solve(scope.Graph);
        if (!states.TryGetValue(scope.Graph.Exit, out var exitState) ||
            (exitState.DepthMask & ~1) == 0)
        {
            return [];
        }

        return exitState.OpenBegins.Keys
            .OrderBy(begin => begin.FirstTokenIndex)
            .Select(begin => new ControlFlowIssue(
                begin,
                "A transaction opened in this scope is not committed or rolled back on every execution path."))
            .ToArray();
    }

    private sealed class TransactionAnalysis : ForwardDataFlowAnalysis<TransactionState>
    {
        protected override TransactionState InitialState() => new(1, false, new());

        protected override TransactionState Transfer(TransactionState input, CfgNode node)
        {
            return node.Statement switch
            {
                BeginTransactionStatement begin => BeginTransaction(input, begin),
                CommitTransactionStatement => CommitTransaction(input),
                RollbackTransactionStatement rollback when TransactionStatementHelpers.IsFullRollback(
                    rollback,
                    input.OpenBegins.Keys.OrderBy(begin => begin.FirstTokenIndex).FirstOrDefault()) =>
                    new TransactionState(1, false, new()),
                ExecuteStatement => new TransactionState(0, true, new()),
                _ => input
            };
        }

        protected override TransactionState Merge(TransactionState left, TransactionState right) =>
            new(
                left.DepthMask | right.DepthMask,
                left.HasUnknownPath || right.HasUnknownPath,
                MergeOpenBegins(left.OpenBegins, right.OpenBegins));

        protected override bool StateEquals(TransactionState left, TransactionState right) =>
            left.DepthMask == right.DepthMask &&
            left.HasUnknownPath == right.HasUnknownPath &&
            left.OpenBegins.Count == right.OpenBegins.Count &&
            left.OpenBegins.All(pair =>
                right.OpenBegins.TryGetValue(pair.Key, out var mask) && mask == pair.Value);

        protected override TransactionState TransferEdge(
            TransactionState input,
            TransactionState output,
            CfgEdge edge)
        {
            if (edge.Source.Statement is IfStatement conditional &&
                BranchGuaranteesNoTransaction(conditional.Predicate, edge.Kind))
            {
                return new TransactionState(1, false, new());
            }
            return base.TransferEdge(input, output, edge);
        }

        private static TransactionState BeginTransaction(
            TransactionState input,
            BeginTransactionStatement begin)
        {
            var openBegins = input.OpenBegins.ToDictionary(
                pair => pair.Key,
                pair => ShiftDepths(pair.Value, increase: true));
            openBegins[begin] = 1;
            return new TransactionState(
                ShiftDepths(input.DepthMask, increase: true) |
                    (input.HasUnknownPath ? 2 : 0),
                input.HasUnknownPath,
                openBegins);
        }

        private static TransactionState CommitTransaction(TransactionState input)
        {
            var openBegins = new Dictionary<BeginTransactionStatement, int>();
            foreach (var (begin, mask) in input.OpenBegins)
            {
                var remainingMask = mask >> 1;
                if (remainingMask != 0)
                {
                    openBegins[begin] = remainingMask;
                }
            }
            return new TransactionState(
                ShiftDepths(input.DepthMask, increase: false),
                input.HasUnknownPath,
                openBegins);
        }

        private static Dictionary<BeginTransactionStatement, int> MergeOpenBegins(
            IReadOnlyDictionary<BeginTransactionStatement, int> left,
            IReadOnlyDictionary<BeginTransactionStatement, int> right)
        {
            var merged = new Dictionary<BeginTransactionStatement, int>(left);
            foreach (var (begin, mask) in right)
            {
                merged[begin] = merged.GetValueOrDefault(begin) | mask;
            }
            return merged;
        }

        private static int ShiftDepths(int mask, bool increase)
        {
            if (increase)
            {
                var result = 0;
                for (var depth = 0; depth <= 8; depth++)
                {
                    if ((mask & (1 << depth)) != 0)
                    {
                        result |= 1 << Math.Min(8, depth + 1);
                    }
                }
                return result;
            }

            var shifted = mask >> 1;
            return (mask & 1) != 0 ? shifted | 1 : shifted;
        }

        private static bool BranchGuaranteesNoTransaction(
            BooleanExpression predicate,
            CfgEdgeKind edgeKind)
        {
            if (predicate is BooleanParenthesisExpression parenthesis)
            {
                return BranchGuaranteesNoTransaction(parenthesis.Expression, edgeKind);
            }
            if (predicate is not BooleanComparisonExpression comparison ||
                edgeKind is not (CfgEdgeKind.TrueBranch or CfgEdgeKind.FalseBranch))
            {
                return false;
            }

            var stateExpression = comparison.FirstExpression;
            var zeroExpression = comparison.SecondExpression;
            var stateIsFirst = true;
            if (!IsTransactionStateExpression(stateExpression) ||
                zeroExpression is not IntegerLiteral { Value: "0" })
            {
                stateExpression = comparison.SecondExpression;
                zeroExpression = comparison.FirstExpression;
                stateIsFirst = false;
                if (!IsTransactionStateExpression(stateExpression) ||
                    zeroExpression is not IntegerLiteral { Value: "0" })
                {
                    return false;
                }
            }

            var trueBranch = edgeKind == CfgEdgeKind.TrueBranch;
            return comparison.ComparisonType switch
            {
                BooleanComparisonType.Equals => trueBranch,
                BooleanComparisonType.NotEqualToBrackets or BooleanComparisonType.NotEqualToExclamation => !trueBranch,
                BooleanComparisonType.GreaterThan when stateIsFirst && IsTransactionCountExpression(stateExpression) =>
                    !trueBranch,
                BooleanComparisonType.LessThan when !stateIsFirst && IsTransactionCountExpression(stateExpression) =>
                    !trueBranch,
                _ => false
            };
        }

        private static bool IsTransactionCountExpression(ScalarExpression expression) =>
            expression is GlobalVariableExpression global &&
            string.Equals(global.Name, "@@TRANCOUNT", StringComparison.OrdinalIgnoreCase);

        private static bool IsTransactionStateExpression(ScalarExpression expression) => expression switch
        {
            GlobalVariableExpression global =>
                string.Equals(global.Name, "@@TRANCOUNT", StringComparison.OrdinalIgnoreCase),
            FunctionCall function =>
                string.Equals(function.FunctionName?.Value, "XACT_STATE", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private sealed record TransactionState(
        int DepthMask,
        bool HasUnknownPath,
        Dictionary<BeginTransactionStatement, int> OpenBegins);
}
