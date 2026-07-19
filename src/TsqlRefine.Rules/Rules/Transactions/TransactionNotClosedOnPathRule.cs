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

        var begin = scope.Graph.Nodes
            .Where(node => states.ContainsKey(node))
            .Select(node => node.Statement)
            .OfType<BeginTransactionStatement>()
            .FirstOrDefault();
        return begin is null
            ? []
            : [new ControlFlowIssue(
                begin,
                "A transaction opened in this scope is not committed or rolled back on every execution path.")];
    }

    private sealed class TransactionAnalysis : ForwardDataFlowAnalysis<TransactionState>
    {
        protected override TransactionState InitialState() => new(1, false);

        protected override TransactionState Transfer(TransactionState input, CfgNode node)
        {
            return node.Statement switch
            {
                BeginTransactionStatement => input with
                {
                    DepthMask = ShiftDepths(input.DepthMask, increase: true) |
                        (input.HasUnknownPath ? 2 : 0)
                },
                CommitTransactionStatement => input with
                {
                    DepthMask = ShiftDepths(input.DepthMask, increase: false)
                },
                RollbackTransactionStatement rollback when rollback.Name is null => input with { DepthMask = 1 },
                ExecuteStatement => new TransactionState(0, true),
                _ => input
            };
        }

        protected override TransactionState Merge(TransactionState left, TransactionState right) =>
            new(left.DepthMask | right.DepthMask, left.HasUnknownPath || right.HasUnknownPath);

        protected override bool StateEquals(TransactionState left, TransactionState right) => left == right;

        protected override TransactionState TransferEdge(
            TransactionState input,
            TransactionState output,
            CfgEdge edge)
        {
            if (edge.Source.Statement is IfStatement conditional &&
                BranchGuaranteesNoTransaction(conditional.Predicate, edge.Kind))
            {
                return new TransactionState(1, false);
            }
            return base.TransferEdge(input, output, edge);
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
            if (!IsTransactionStateExpression(stateExpression) ||
                zeroExpression is not IntegerLiteral { Value: "0" })
            {
                stateExpression = comparison.SecondExpression;
                zeroExpression = comparison.FirstExpression;
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
                _ => false
            };
        }

        private static bool IsTransactionStateExpression(ScalarExpression expression) => expression switch
        {
            GlobalVariableExpression global =>
                string.Equals(global.Name, "@@TRANCOUNT", StringComparison.OrdinalIgnoreCase),
            FunctionCall function =>
                string.Equals(function.FunctionName?.Value, "XACT_STATE", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private sealed record TransactionState(int DepthMask, bool HasUnknownPath);
}
