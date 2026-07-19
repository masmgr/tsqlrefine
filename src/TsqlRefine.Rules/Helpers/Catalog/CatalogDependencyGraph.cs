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

    private int GetViewNestingDepth(
        CatalogObjectInfo current,
        HashSet<CatalogObjectInfo> active,
        Dictionary<CatalogObjectInfo, int> memo)
    {
        if (memo.TryGetValue(current, out var cached))
        {
            return cached;
        }
        if (!active.Add(current))
        {
            return 0;
        }
        var depth = GetDependencies(current)
            .Where(dependency => dependency.Kind == CatalogObjectKind.View)
            .Select(dependency => 1 + GetViewNestingDepth(dependency, active, memo))
            .DefaultIfEmpty(0)
            .Max();
        active.Remove(current);
        memo[current] = depth;
        return depth;
    }

    private int GetCachedViewNestingDepth(CatalogObjectInfo view)
    {
        lock (_viewDepths)
        {
            return GetViewNestingDepth(view, [], _viewDepths);
        }
    }

    private Dictionary<CatalogObjectInfo, IReadOnlyList<CatalogObjectInfo>> FindCycles()
    {
        var index = 0;
        var indexes = new Dictionary<CatalogObjectInfo, int>();
        var lowLinks = new Dictionary<CatalogObjectInfo, int>();
        var stack = new Stack<CatalogObjectInfo>();
        var onStack = new HashSet<CatalogObjectInfo>();
        var cycles = new Dictionary<CatalogObjectInfo, IReadOnlyList<CatalogObjectInfo>>();

        foreach (var obj in Objects)
        {
            if (!indexes.ContainsKey(obj))
            {
                Visit(obj);
            }
        }
        return cycles;

        void Visit(CatalogObjectInfo current)
        {
            indexes[current] = index;
            lowLinks[current] = index;
            index++;
            stack.Push(current);
            onStack.Add(current);

            foreach (var dependency in GetDependencies(current))
            {
                if (!indexes.TryGetValue(dependency, out var dependencyIndex))
                {
                    Visit(dependency);
                    lowLinks[current] = Math.Min(lowLinks[current], lowLinks[dependency]);
                }
                else if (onStack.Contains(dependency))
                {
                    lowLinks[current] = Math.Min(lowLinks[current], dependencyIndex);
                }
            }

            if (lowLinks[current] != indexes[current])
            {
                return;
            }

            var component = new List<CatalogObjectInfo>();
            CatalogObjectInfo member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                component.Add(member);
            }
            while (member != current);

            if (component.Count > 1 || GetDependencies(current).Contains(current))
            {
                var start = component[0];
                var path = new List<CatalogObjectInfo> { start };
                var active = new HashSet<CatalogObjectInfo> { start };
                var cycle = FindCycleCore(start, start, path, active)!;
                foreach (var item in component)
                {
                    cycles[item] = cycle;
                }
            }
        }
    }

    private IReadOnlyList<CatalogObjectInfo>? FindCycleCore(
        CatalogObjectInfo start,
        CatalogObjectInfo current,
        List<CatalogObjectInfo> path,
        HashSet<CatalogObjectInfo> active)
    {
        foreach (var dependency in GetDependencies(current))
        {
            if (dependency == start)
            {
                return [.. path, start];
            }
            if (!active.Add(dependency))
            {
                continue;
            }
            path.Add(dependency);
            var cycle = FindCycleCore(start, dependency, path, active);
            if (cycle is not null)
            {
                return cycle;
            }
            path.RemoveAt(path.Count - 1);
            active.Remove(dependency);
        }
        return null;
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
