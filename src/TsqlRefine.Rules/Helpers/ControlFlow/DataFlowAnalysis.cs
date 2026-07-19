namespace TsqlRefine.Rules.Helpers.ControlFlow;

/// <summary>Worklist-based framework for finite forward may/must analyses.</summary>
public abstract class ForwardDataFlowAnalysis<TState>
{
    protected abstract TState InitialState();
    protected abstract TState Transfer(TState input, CfgNode node);
    protected abstract TState Merge(TState left, TState right);
    protected abstract bool StateEquals(TState left, TState right);

    /// <summary>Computes the input state for every reachable graph node.</summary>
    public IReadOnlyDictionary<CfgNode, TState> Solve(ControlFlowGraph cfg)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        var states = new Dictionary<CfgNode, TState>
        {
            [cfg.Entry] = InitialState()
        };
        var worklist = new Queue<CfgNode>();
        var queued = new HashSet<CfgNode>();
        worklist.Enqueue(cfg.Entry);
        queued.Add(cfg.Entry);

        while (worklist.Count > 0)
        {
            var node = worklist.Dequeue();
            queued.Remove(node);
            var output = Transfer(states[node], node);
            foreach (var edge in node.Successors)
            {
                var changed = false;
                if (states.TryGetValue(edge.Target, out var existing))
                {
                    var merged = Merge(existing, output);
                    if (!StateEquals(existing, merged))
                    {
                        states[edge.Target] = merged;
                        changed = true;
                    }
                }
                else
                {
                    states[edge.Target] = output;
                    changed = true;
                }

                if (changed && queued.Add(edge.Target))
                {
                    worklist.Enqueue(edge.Target);
                }
            }
        }
        return states;
    }
}
