using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.ControlFlow;

/// <summary>Builds structured control-flow graphs from ScriptDOM statements.</summary>
public static class ControlFlowGraphBuilder
{
    public static CfgBuildResult Build(TSqlBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return BuildCore(batch.Statements);
    }

    public static CfgBuildResult Build(StatementList body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return BuildCore(body.Statements);
    }

    private static CfgBuildResult BuildCore(IList<TSqlStatement> statements)
    {
        var unsupportedVisitor = new UnsupportedControlFlowVisitor();
        foreach (var statement in statements)
        {
            statement.Accept(unsupportedVisitor);
        }

        var builder = new Builder();
        var exits = builder.BuildSequence(
            statements,
            [new FlowEndpoint(builder.Entry, CfgEdgeKind.Sequential)],
            new FlowContext(null, null, null));
        foreach (var endpoint in exits)
        {
            Builder.Connect(endpoint.Node, builder.Exit, endpoint.EdgeKind);
        }

        return new CfgBuildResult(
            new ControlFlowGraph(builder.Entry, builder.Exit, builder.Nodes),
            unsupportedVisitor.Reasons.ToArray());
    }

    private sealed class Builder
    {
        private readonly List<CfgNode> _nodes = [];

        internal Builder()
        {
            Entry = CreateNode(CfgNodeKind.Entry, null);
            Exit = CreateNode(CfgNodeKind.Exit, null);
        }

        internal CfgNode Entry { get; }
        internal CfgNode Exit { get; }
        internal IReadOnlyList<CfgNode> Nodes => _nodes;

        internal IReadOnlyList<FlowEndpoint> BuildSequence(
            IList<TSqlStatement> statements,
            IReadOnlyList<FlowEndpoint> incoming,
            FlowContext context)
        {
            var current = incoming;
            foreach (var statement in statements)
            {
                current = BuildStatement(statement, current, context);
            }
            return current;
        }

        private IReadOnlyList<FlowEndpoint> BuildStatement(
            TSqlStatement statement,
            IReadOnlyList<FlowEndpoint> incoming,
            FlowContext context) => statement switch
            {
                BeginEndBlockStatement block => BuildSequence(block.StatementList.Statements, incoming, context),
                IfStatement conditional => BuildIf(conditional, incoming, context),
                WhileStatement loop => BuildWhile(loop, incoming, context),
                TryCatchStatement tryCatch => BuildTryCatch(tryCatch, incoming, context),
                ReturnStatement => BuildTerminator(statement, incoming, Exit, CfgEdgeKind.Return),
                ThrowStatement => BuildTerminator(
                    statement,
                    incoming,
                    context.ExceptionTarget ?? Exit,
                    CfgEdgeKind.Exception),
                BreakStatement when context.BreakTarget is not null => BuildTerminator(
                    statement,
                    incoming,
                    context.BreakTarget,
                    CfgEdgeKind.Break),
                ContinueStatement when context.ContinueTarget is not null => BuildTerminator(
                    statement,
                    incoming,
                    context.ContinueTarget,
                    CfgEdgeKind.Continue),
                RaiseErrorStatement raiseError => BuildRaiseError(raiseError, incoming, context),
                _ => BuildOrdinary(statement, incoming, context)
            };

        private IReadOnlyList<FlowEndpoint> BuildIf(
            IfStatement statement,
            IReadOnlyList<FlowEndpoint> incoming,
            FlowContext context)
        {
            var condition = CreateNode(CfgNodeKind.Statement, statement);
            ConnectIncoming(incoming, condition);
            var join = CreateNode(CfgNodeKind.Join, null);

            var thenExits = BuildStatement(
                statement.ThenStatement,
                [new FlowEndpoint(condition, CfgEdgeKind.TrueBranch)],
                context);
            ConnectIncoming(thenExits, join);

            if (statement.ElseStatement is null)
            {
                Connect(condition, join, CfgEdgeKind.FalseBranch);
            }
            else
            {
                var elseExits = BuildStatement(
                    statement.ElseStatement,
                    [new FlowEndpoint(condition, CfgEdgeKind.FalseBranch)],
                    context);
                ConnectIncoming(elseExits, join);
            }
            return [new FlowEndpoint(join, CfgEdgeKind.Sequential)];
        }

        private IReadOnlyList<FlowEndpoint> BuildWhile(
            WhileStatement statement,
            IReadOnlyList<FlowEndpoint> incoming,
            FlowContext context)
        {
            var condition = CreateNode(CfgNodeKind.Statement, statement);
            var join = CreateNode(CfgNodeKind.Join, null);
            ConnectIncoming(incoming, condition);
            Connect(condition, join, CfgEdgeKind.FalseBranch);

            var loopContext = context with { BreakTarget = join, ContinueTarget = condition };
            var bodyExits = BuildStatement(
                statement.Statement,
                [new FlowEndpoint(condition, CfgEdgeKind.TrueBranch)],
                loopContext);
            foreach (var endpoint in bodyExits)
            {
                Connect(endpoint.Node, condition, CfgEdgeKind.LoopBack);
            }
            return [new FlowEndpoint(join, CfgEdgeKind.Sequential)];
        }

        private IReadOnlyList<FlowEndpoint> BuildTryCatch(
            TryCatchStatement statement,
            IReadOnlyList<FlowEndpoint> incoming,
            FlowContext context)
        {
            var dispatch = CreateNode(CfgNodeKind.Join, null);
            var catchEntry = CreateNode(CfgNodeKind.Join, null);
            var join = CreateNode(CfgNodeKind.Join, null);
            ConnectIncoming(incoming, dispatch);

            var tryContext = context with { ExceptionTarget = catchEntry };
            var tryExits = BuildSequence(
                statement.TryStatements.Statements,
                [new FlowEndpoint(dispatch, CfgEdgeKind.Sequential)],
                tryContext);
            ConnectIncoming(tryExits, join);

            var catchExits = BuildSequence(
                statement.CatchStatements.Statements,
                [new FlowEndpoint(catchEntry, CfgEdgeKind.Sequential)],
                context);
            ConnectIncoming(catchExits, join);
            return [new FlowEndpoint(join, CfgEdgeKind.Sequential)];
        }

        private IReadOnlyList<FlowEndpoint> BuildRaiseError(
            RaiseErrorStatement statement,
            IReadOnlyList<FlowEndpoint> incoming,
            FlowContext context)
        {
            var node = CreateNode(CfgNodeKind.Statement, statement);
            ConnectIncoming(incoming, node);
            if (context.ExceptionTarget is null)
            {
                return [new FlowEndpoint(node, CfgEdgeKind.Sequential)];
            }

            var severity = (statement.SecondParameter as IntegerLiteral)?.Value;
            if (int.TryParse(severity, out var value) && value < 11)
            {
                return [new FlowEndpoint(node, CfgEdgeKind.Sequential)];
            }

            var conservative = severity is null || !int.TryParse(severity, out _);
            Connect(node, context.ExceptionTarget, CfgEdgeKind.Exception, conservative);
            return conservative ? [new FlowEndpoint(node, CfgEdgeKind.Sequential)] : [];
        }

        private IReadOnlyList<FlowEndpoint> BuildOrdinary(
            TSqlStatement statement,
            IReadOnlyList<FlowEndpoint> incoming,
            FlowContext context)
        {
            var node = CreateNode(CfgNodeKind.Statement, statement);
            ConnectIncoming(incoming, node);
            if (context.ExceptionTarget is not null)
            {
                Connect(node, context.ExceptionTarget, CfgEdgeKind.Exception, isConservative: true);
            }
            return [new FlowEndpoint(node, CfgEdgeKind.Sequential)];
        }

        private IReadOnlyList<FlowEndpoint> BuildTerminator(
            TSqlStatement statement,
            IReadOnlyList<FlowEndpoint> incoming,
            CfgNode target,
            CfgEdgeKind edgeKind)
        {
            var node = CreateNode(CfgNodeKind.Statement, statement);
            ConnectIncoming(incoming, node);
            Connect(node, target, edgeKind);
            return [];
        }

        private CfgNode CreateNode(CfgNodeKind kind, TSqlStatement? statement)
        {
            var node = new CfgNode(_nodes.Count, kind, statement);
            _nodes.Add(node);
            return node;
        }

        private static void ConnectIncoming(IReadOnlyList<FlowEndpoint> incoming, CfgNode target)
        {
            foreach (var endpoint in incoming)
            {
                Connect(endpoint.Node, target, endpoint.EdgeKind);
            }
        }

        internal static void Connect(
            CfgNode source,
            CfgNode target,
            CfgEdgeKind kind,
            bool isConservative = false)
        {
            var edge = new CfgEdge(source, target, kind, isConservative);
            source.AddSuccessor(edge);
            target.AddPredecessor(edge);
        }
    }

    private sealed class UnsupportedControlFlowVisitor : TSqlFragmentVisitor
    {
        internal HashSet<string> Reasons { get; } = new(StringComparer.Ordinal);

        public override void ExplicitVisit(GoToStatement node)
        {
            Reasons.Add("GOTO statements are not supported by control-flow analysis.");
            base.ExplicitVisit(node);
        }
    }

    private sealed record FlowEndpoint(CfgNode Node, CfgEdgeKind EdgeKind);

    private sealed record FlowContext(
        CfgNode? BreakTarget,
        CfgNode? ContinueTarget,
        CfgNode? ExceptionTarget);
}
