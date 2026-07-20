using System.Runtime.CompilerServices;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.ControlFlow;

/// <summary>A batch, procedure, or function body analyzed as an independent flow scope.</summary>
public sealed record ControlFlowScope(
    TSqlFragment Owner,
    ControlFlowGraph Graph,
    IReadOnlyList<string> UnsupportedReasons,
    IReadOnlyList<ProcedureParameter> Parameters)
{
    public bool IsSupported => UnsupportedReasons.Count == 0;
}

/// <summary>Finds independent executable scopes and builds a graph for each one.</summary>
public static class ControlFlowScopeCollector
{
    private static readonly ConditionalWeakTable<TSqlFragment, ControlFlowScope[]> s_cache = new();

    public static IReadOnlyList<ControlFlowScope> Collect(TSqlFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        return s_cache.GetValue(fragment, static value => CollectCore(value));
    }

    private static ControlFlowScope[] CollectCore(TSqlFragment fragment)
    {
        if (fragment is not TSqlScript script)
        {
            return [];
        }

        var scopes = new List<ControlFlowScope>();
        foreach (var batch in script.Batches)
        {
            var containsRoutine = false;
            foreach (var statement in batch.Statements)
            {
                if (TryGetRoutineBody(statement, out var owner, out var body, out var parameters))
                {
                    containsRoutine = true;
                    var result = ControlFlowGraphBuilder.Build(body);
                    scopes.Add(new ControlFlowScope(
                        owner,
                        result.Graph,
                        result.UnsupportedReasons,
                        parameters));
                }
            }

            if (!containsRoutine)
            {
                var result = ControlFlowGraphBuilder.Build(batch);
                scopes.Add(new ControlFlowScope(batch, result.Graph, result.UnsupportedReasons, []));
            }
        }
        return scopes.ToArray();
    }

    private static bool TryGetRoutineBody(
        TSqlStatement statement,
        out TSqlFragment owner,
        out StatementList body,
        out IReadOnlyList<ProcedureParameter> parameters)
    {
        switch (statement)
        {
            case ProcedureStatementBody procedure:
                owner = procedure;
                body = procedure.StatementList;
                parameters = procedure.Parameters.ToArray();
                return true;
            case FunctionStatementBody function when function.StatementList is not null:
                owner = function;
                body = function.StatementList;
                parameters = function.Parameters.ToArray();
                return true;
            case TriggerStatementBody trigger when trigger.StatementList is not null:
                owner = trigger;
                body = trigger.StatementList;
                parameters = [];
                return true;
            default:
                owner = null!;
                body = null!;
                parameters = [];
                return false;
        }
    }
}
