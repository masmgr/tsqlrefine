using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.Rules.Helpers.ControlFlow;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class ControlFlowGraphTests
{
    [Fact]
    public void Build_SequentialStatements_ConnectsEntryStatementsAndExit()
    {
        var result = Build("SELECT 1; SELECT 2;");
        var statements = result.Graph.Nodes.Where(node => node.Statement is SelectStatement).ToArray();

        Assert.True(result.IsSupported);
        Assert.Equal(2, statements.Length);
        Assert.Contains(result.Graph.Entry.Successors, edge => edge.Target == statements[0]);
        Assert.Contains(statements[0].Successors, edge => edge.Target == statements[1]);
        Assert.Contains(statements[1].Successors, edge => edge.Target == result.Graph.Exit);
    }

    [Fact]
    public void Build_IfElse_CreatesTrueAndFalseBranchesThatJoin()
    {
        var result = Build("IF @flag = 1 SELECT 1; ELSE SELECT 2; SELECT 3;");
        var condition = Assert.Single(result.Graph.Nodes, node => node.Statement is IfStatement);

        Assert.Contains(condition.Successors, edge => edge.Kind == CfgEdgeKind.TrueBranch);
        Assert.Contains(condition.Successors, edge => edge.Kind == CfgEdgeKind.FalseBranch);
        var branchTargets = condition.Successors.Select(edge => edge.Target).ToArray();
        Assert.All(branchTargets, target => Assert.IsType<SelectStatement>(target.Statement));
        Assert.Same(
            branchTargets[0].Successors.Single().Target,
            branchTargets[1].Successors.Single().Target);
    }

    [Fact]
    public void Build_WhileWithContinueAndBreak_CreatesLoopControlEdges()
    {
        var result = Build("WHILE @run = 1 BEGIN IF @skip = 1 CONTINUE; BREAK; END; SELECT 1;");

        Assert.Contains(result.Graph.Nodes.SelectMany(node => node.Successors), edge =>
            edge.Kind == CfgEdgeKind.Continue);
        Assert.Contains(result.Graph.Nodes.SelectMany(node => node.Successors), edge =>
            edge.Kind == CfgEdgeKind.Break);
    }

    [Fact]
    public void Build_TryCatch_AddsConservativeExceptionEdgeForOrdinaryStatement()
    {
        var result = Build("BEGIN TRY SELECT 1; END TRY BEGIN CATCH SELECT 2; END CATCH;");
        var firstSelect = result.Graph.Nodes.First(node => node.Statement is SelectStatement);

        var edge = Assert.Single(firstSelect.Successors, candidate => candidate.Kind == CfgEdgeKind.Exception);
        Assert.True(edge.IsConservative);
    }

    [Fact]
    public void Build_Return_LeavesFollowingStatementUnreachable()
    {
        var result = Build("RETURN; SELECT 1;");
        var reachable = ReachableNodes(result.Graph);
        var select = Assert.Single(result.Graph.Nodes, node => node.Statement is SelectStatement);

        Assert.DoesNotContain(select, reachable);
        Assert.Contains(result.Graph.Exit, reachable);
    }

    [Fact]
    public void Build_Goto_RecordsUnsupportedReason()
    {
        var result = Build("GOTO finish; finish: SELECT 1;");

        Assert.False(result.IsSupported);
        Assert.Contains(result.UnsupportedReasons, reason => reason.Contains("GOTO", StringComparison.Ordinal));
    }

    [Fact]
    public void ForwardAnalysis_Loop_ConvergesToFixedPoint()
    {
        var result = Build("DECLARE @x int; WHILE @x < 2 SET @x += 1;");

        var states = new StatementCountAnalysis().Solve(result.Graph);

        Assert.True(states.ContainsKey(result.Graph.Exit));
        Assert.True(states[result.Graph.Exit] > 0);
    }

    private static CfgBuildResult Build(string sql)
    {
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql);
        var script = Assert.IsType<TSqlScript>(parser.Parse(reader, out var errors));
        Assert.Empty(errors);
        return ControlFlowGraphBuilder.Build(Assert.Single(script.Batches));
    }

    private static HashSet<CfgNode> ReachableNodes(ControlFlowGraph graph)
    {
        var reachable = new HashSet<CfgNode>();
        var pending = new Stack<CfgNode>();
        pending.Push(graph.Entry);
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
        return reachable;
    }

    private sealed class StatementCountAnalysis : ForwardDataFlowAnalysis<int>
    {
        protected override int InitialState() => 0;

        protected override int Transfer(int input, CfgNode node) =>
            node.Kind == CfgNodeKind.Statement ? Math.Min(3, input + 1) : input;

        protected override int Merge(int left, int right) => Math.Max(left, right);

        protected override bool StateEquals(int left, int right) => left == right;
    }
}
