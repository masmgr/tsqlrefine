using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Catalog;

namespace TsqlRefine.Rules.Rules.Performance;

public sealed class DeepViewNestingRule : IRule, IRuleOptionsDescriptorProvider
{
    private const int DefaultMaximum = 3;

    public RuleMetadata Metadata { get; } = new(
        "deep-view-nesting",
        "Detects views whose dependency nesting exceeds a configured maximum.",
        "Performance",
        RuleSeverity.Warning,
        false);

    public IReadOnlyList<RuleOptionDescriptor> OptionDescriptors { get; } =
    [
        new("max", RuleOptionType.Number, "Maximum allowed view dependency depth.", 1, 10000)
    ];

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ObjectCatalog is not { HasData: true } catalog)
        {
            return [];
        }
        var maximum = context.Settings.Options?.TryGetInt32("max", out var configured) is true
            ? configured
            : DefaultMaximum;
        var graph = CatalogDependencyGraph.For(catalog);
        return graph.Objects
            .Where(obj => obj.Kind == CatalogObjectKind.View &&
                          CatalogFilePathHelpers.SameFile(obj.DefinedInFile, context.FilePath))
            .Select(obj => (Object: obj, Depth: graph.GetViewNestingDepth(obj)))
            .Where(item => item.Depth > maximum)
            .Select(item => new Diagnostic(
                item.Object.DefinedAt,
                $"View '{CatalogDependencyGraph.DisplayName(item.Object.Id)}' has nesting depth {item.Depth}, exceeding the configured maximum of {maximum}.",
                Code: Metadata.RuleId,
                Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, false)))
            .ToArray();
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

}
