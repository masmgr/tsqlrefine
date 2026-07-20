using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.ControlFlow;

/// <summary>Kind of node in a control-flow graph.</summary>
public enum CfgNodeKind
{
    Entry,
    Statement,
    Join,
    Exit
}

/// <summary>Meaning of a directed control-flow edge.</summary>
public enum CfgEdgeKind
{
    Sequential,
    TrueBranch,
    FalseBranch,
    LoopBack,
    Exception,
    Return,
    Break,
    Continue
}

/// <summary>A directed edge between control-flow nodes.</summary>
public sealed record CfgEdge(
    CfgNode Source,
    CfgNode Target,
    CfgEdgeKind Kind,
    bool IsConservative = false);

/// <summary>A statement or synthetic point in a control-flow graph.</summary>
public sealed class CfgNode
{
    private readonly List<CfgEdge> _successors = [];
    private readonly List<CfgEdge> _predecessors = [];

    internal CfgNode(int id, CfgNodeKind kind, TSqlStatement? statement)
    {
        Id = id;
        Kind = kind;
        Statement = statement;
    }

    public int Id { get; }
    public CfgNodeKind Kind { get; }
    public TSqlStatement? Statement { get; }
    public IReadOnlyList<CfgEdge> Successors => _successors;
    public IReadOnlyList<CfgEdge> Predecessors => _predecessors;

    internal void AddSuccessor(CfgEdge edge) => _successors.Add(edge);
    internal void AddPredecessor(CfgEdge edge) => _predecessors.Add(edge);
}

/// <summary>Control-flow graph for one batch or routine body.</summary>
public sealed record ControlFlowGraph(
    CfgNode Entry,
    CfgNode Exit,
    IReadOnlyList<CfgNode> Nodes);

/// <summary>Graph construction result and reasons that make flow analysis unsafe.</summary>
public sealed record CfgBuildResult(
    ControlFlowGraph Graph,
    IReadOnlyList<string> UnsupportedReasons)
{
    public bool IsSupported => UnsupportedReasons.Count == 0;
}
