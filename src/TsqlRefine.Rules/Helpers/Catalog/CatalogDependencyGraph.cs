using System.Runtime.CompilerServices;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Catalog;

internal sealed class CatalogDependencyGraph
{
    private static readonly ConditionalWeakTable<IObjectCatalogProvider, CatalogDependencyGraph> Cache = new();
    private readonly Dictionary<string, CatalogObjectInfo> _objectsById;
    private readonly Dictionary<CatalogObjectInfo, CatalogObjectInfo[]> _outgoing;

    private CatalogDependencyGraph(IObjectCatalogProvider catalog)
    {
        Objects = catalog.GetAllObjects();
        _objectsById = Objects.ToDictionary(obj => CreateKey(obj.Id), StringComparer.OrdinalIgnoreCase);
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
    }

    internal IReadOnlyList<CatalogObjectInfo> Objects { get; }

    internal static CatalogDependencyGraph For(IObjectCatalogProvider catalog) =>
        Cache.GetValue(catalog, static provider => new CatalogDependencyGraph(provider));

    internal IReadOnlyList<CatalogObjectInfo> GetDependencies(CatalogObjectInfo source) =>
        _outgoing.TryGetValue(source, out var dependencies) ? dependencies : [];

    internal int GetViewNestingDepth(CatalogObjectInfo view) =>
        GetViewNestingDepth(view, [], new Dictionary<CatalogObjectInfo, int>());

    internal IReadOnlyList<CatalogObjectInfo>? FindCycle(CatalogObjectInfo start)
    {
        var path = new List<CatalogObjectInfo> { start };
        var active = new HashSet<CatalogObjectInfo> { start };
        return FindCycleCore(start, start, path, active);
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
}
