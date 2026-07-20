using System.Runtime.CompilerServices;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Catalog;

internal sealed class CatalogDependencyGraph
{
    private static readonly ConditionalWeakTable<IObjectCatalogProvider, CatalogDependencyGraph> Cache = new();
    private readonly Dictionary<string, CatalogObjectInfo> _objectsById;
    private readonly Dictionary<string, int> _objectCountsByKind;
    private readonly Dictionary<CatalogObjectInfo, CatalogObjectInfo[]> _outgoing;
    private readonly Dictionary<CatalogObjectInfo, int> _viewDepths = [];
    private readonly Dictionary<CatalogObjectInfo, IReadOnlyList<CatalogObjectInfo>> _cyclesByObject;

    private CatalogDependencyGraph(IObjectCatalogProvider catalog)
    {
        Objects = catalog.GetAllObjects();
        _objectsById = Objects.ToDictionary(obj => CreateKey(obj.Id), StringComparer.OrdinalIgnoreCase);
        _objectCountsByKind = Objects
            .GroupBy(obj => CreateKindKey(obj.Id, obj.Kind), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var outgoing = Objects.ToDictionary(obj => obj, _ => new HashSet<CatalogObjectInfo>());
        foreach (var target in Objects)
        {
            foreach (var reference in catalog.GetReferencesTo(
                         target.Id.DatabaseName,
                         target.Id.SchemaName,
                         target.Id.Name))
            {
                if (reference.Resolution != CatalogResolutionStatus.Resolved ||
                    reference.FromObject is null ||
                    !_objectsById.TryGetValue(CreateKey(reference.FromObject), out var source))
                {
                    continue;
                }
                outgoing[source].Add(target);
            }
        }
        _outgoing = outgoing.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        _cyclesByObject = FindCycles();
    }

    internal IReadOnlyList<CatalogObjectInfo> Objects { get; }

    internal static CatalogDependencyGraph For(IObjectCatalogProvider catalog) =>
        Cache.GetValue(catalog, static provider => new CatalogDependencyGraph(provider));

    internal IReadOnlyList<CatalogObjectInfo> GetDependencies(CatalogObjectInfo source) =>
        _outgoing.TryGetValue(source, out var dependencies) ? dependencies : [];

    internal int GetViewNestingDepth(CatalogObjectInfo view) =>
        GetCachedViewNestingDepth(view);

    internal int CountMatches(
        string? database,
        string schema,
        string name,
        CatalogObjectKindFilter filter)
    {
        var id = new CatalogObjectIdInfo(database, schema, name);
        var count = 0;
        foreach (var kind in Enum.GetValues<CatalogObjectKind>())
        {
            if ((filter & ToFilter(kind)) != 0)
            {
                count += _objectCountsByKind.GetValueOrDefault(CreateKindKey(id, kind));
            }
        }
        return count;
    }

    internal IReadOnlyList<CatalogObjectInfo>? FindCycle(CatalogObjectInfo start)
    {
        if (!_cyclesByObject.TryGetValue(start, out var cycle))
        {
            return null;
        }
        var startIndex = -1;
        for (var i = 0; i < cycle.Count - 1; i++)
        {
            if (cycle[i] == start)
            {
                startIndex = i;
                break;
            }
        }
        if (startIndex <= 0)
        {
            return cycle;
        }
        return cycle
            .Skip(startIndex)
            .Take(cycle.Count - startIndex - 1)
            .Concat(cycle.Take(startIndex + 1))
            .ToArray();
    }

    internal static bool IdentityEquals(CatalogObjectIdInfo left, CatalogObjectIdInfo right) =>
        string.Equals(left.DatabaseName ?? string.Empty, right.DatabaseName ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.SchemaName, right.SchemaName, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);

    internal static string DisplayName(CatalogObjectIdInfo id) =>
        string.IsNullOrWhiteSpace(id.DatabaseName)
            ? $"{id.SchemaName}.{id.Name}"
            : $"{id.DatabaseName}.{id.SchemaName}.{id.Name}";

    private int GetCachedViewNestingDepth(CatalogObjectInfo view)
    {
        lock (_viewDepths)
        {
            if (_viewDepths.TryGetValue(view, out var cached))
            {
                return cached;
            }

            var active = new HashSet<CatalogObjectInfo>();
            var frames = new Stack<DepthFrame>();
            frames.Push(new DepthFrame(view, GetViewDependencies(view)));
            active.Add(view);
            while (frames.TryPeek(out var frame))
            {
                if (frame.Index < frame.Dependencies.Length)
                {
                    var dependency = frame.Dependencies[frame.Index++];
                    if (_viewDepths.TryGetValue(dependency, out var dependencyDepth))
                    {
                        frame.MaxDepth = Math.Max(frame.MaxDepth, 1 + dependencyDepth);
                    }
                    else if (active.Add(dependency))
                    {
                        frames.Push(new DepthFrame(dependency, GetViewDependencies(dependency)));
                    }
                    else
                    {
                        frame.MaxDepth = Math.Max(frame.MaxDepth, 1);
                    }
                    continue;
                }

                frames.Pop();
                active.Remove(frame.Object);
                _viewDepths[frame.Object] = frame.MaxDepth;
                if (frames.TryPeek(out var parent))
                {
                    parent.MaxDepth = Math.Max(parent.MaxDepth, 1 + frame.MaxDepth);
                }
            }
            return _viewDepths[view];
        }
    }

    private CatalogObjectInfo[] GetViewDependencies(CatalogObjectInfo view) =>
        GetDependencies(view).Where(dependency => dependency.Kind == CatalogObjectKind.View).ToArray();

    private Dictionary<CatalogObjectInfo, IReadOnlyList<CatalogObjectInfo>> FindCycles()
    {
        var index = 0;
        var indexes = new Dictionary<CatalogObjectInfo, int>();
        var lowLinks = new Dictionary<CatalogObjectInfo, int>();
        var stack = new Stack<CatalogObjectInfo>();
        var onStack = new HashSet<CatalogObjectInfo>();
        var cycles = new Dictionary<CatalogObjectInfo, IReadOnlyList<CatalogObjectInfo>>();

        foreach (var root in Objects)
        {
            if (indexes.ContainsKey(root))
            {
                continue;
            }
            var traversal = new Stack<TarjanFrame>();
            traversal.Push(CreateFrame(root, null));
            while (traversal.TryPeek(out var frame))
            {
                if (frame.Index < frame.Dependencies.Length)
                {
                    var dependency = frame.Dependencies[frame.Index++];
                    if (!indexes.TryGetValue(dependency, out var dependencyIndex))
                    {
                        traversal.Push(CreateFrame(dependency, frame.Object));
                    }
                    else if (onStack.Contains(dependency))
                    {
                        lowLinks[frame.Object] = Math.Min(lowLinks[frame.Object], dependencyIndex);
                    }
                    continue;
                }

                traversal.Pop();
                if (frame.Parent is not null)
                {
                    lowLinks[frame.Parent] = Math.Min(lowLinks[frame.Parent], lowLinks[frame.Object]);
                }
                if (lowLinks[frame.Object] != indexes[frame.Object])
                {
                    continue;
                }

                var component = new List<CatalogObjectInfo>();
                CatalogObjectInfo member;
                do
                {
                    member = stack.Pop();
                    onStack.Remove(member);
                    component.Add(member);
                }
                while (member != frame.Object);

                if (component.Count > 1 || GetDependencies(frame.Object).Contains(frame.Object))
                {
                    var componentSet = component.ToHashSet();
                    foreach (var item in component)
                    {
                        cycles[item] = FindCycleCore(item, componentSet)!;
                    }
                }
            }
        }
        return cycles;

        TarjanFrame CreateFrame(CatalogObjectInfo current, CatalogObjectInfo? parent)
        {
            indexes[current] = index;
            lowLinks[current] = index;
            index++;
            stack.Push(current);
            onStack.Add(current);
            return new TarjanFrame(current, parent, GetDependencies(current).ToArray());
        }
    }

    private IReadOnlyList<CatalogObjectInfo>? FindCycleCore(
        CatalogObjectInfo start,
        IReadOnlySet<CatalogObjectInfo> component)
    {
        var path = new List<CatalogObjectInfo> { start };
        var active = new HashSet<CatalogObjectInfo> { start };
        var frames = new Stack<CycleFrame>();
        frames.Push(new CycleFrame(start, GetDependencies(start).Where(component.Contains).ToArray()));
        while (frames.TryPeek(out var frame))
        {
            if (frame.Index >= frame.Dependencies.Length)
            {
                frames.Pop();
                if (frame.Object != start)
                {
                    active.Remove(frame.Object);
                    path.RemoveAt(path.Count - 1);
                }
                continue;
            }

            var dependency = frame.Dependencies[frame.Index++];
            if (dependency == start)
            {
                return [.. path, start];
            }
            if (!active.Add(dependency))
            {
                continue;
            }
            path.Add(dependency);
            frames.Push(new CycleFrame(
                dependency,
                GetDependencies(dependency).Where(component.Contains).ToArray()));
        }
        return null;
    }

    private sealed class DepthFrame(CatalogObjectInfo obj, CatalogObjectInfo[] dependencies)
    {
        internal CatalogObjectInfo Object { get; } = obj;
        internal CatalogObjectInfo[] Dependencies { get; } = dependencies;
        internal int Index { get; set; }
        internal int MaxDepth { get; set; }
    }

    private sealed class TarjanFrame(
        CatalogObjectInfo obj,
        CatalogObjectInfo? parent,
        CatalogObjectInfo[] dependencies)
    {
        internal CatalogObjectInfo Object { get; } = obj;
        internal CatalogObjectInfo? Parent { get; } = parent;
        internal CatalogObjectInfo[] Dependencies { get; } = dependencies;
        internal int Index { get; set; }
    }

    private sealed class CycleFrame(CatalogObjectInfo obj, CatalogObjectInfo[] dependencies)
    {
        internal CatalogObjectInfo Object { get; } = obj;
        internal CatalogObjectInfo[] Dependencies { get; } = dependencies;
        internal int Index { get; set; }
    }

    private static string CreateKey(CatalogObjectIdInfo id) =>
        $"{id.DatabaseName ?? string.Empty}\u001f{id.SchemaName}\u001f{id.Name}";

    private static string CreateKindKey(CatalogObjectIdInfo id, CatalogObjectKind kind) =>
        $"{CreateKey(id)}\u001f{kind}";

    private static CatalogObjectKindFilter ToFilter(CatalogObjectKind kind) => kind switch
    {
        CatalogObjectKind.Procedure => CatalogObjectKindFilter.Procedure,
        CatalogObjectKind.ScalarFunction => CatalogObjectKindFilter.ScalarFunction,
        CatalogObjectKind.TableValuedFunction => CatalogObjectKindFilter.TableValuedFunction,
        CatalogObjectKind.View => CatalogObjectKindFilter.View,
        _ => CatalogObjectKindFilter.None
    };
}
