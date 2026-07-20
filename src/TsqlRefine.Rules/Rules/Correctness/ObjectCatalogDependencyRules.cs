using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Catalog;

namespace TsqlRefine.Rules.Rules.Correctness;

public sealed class UnresolvedProcedureReferenceRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        "unresolved-procedure-reference",
        "Detects procedure or function calls that do not resolve in an authoritative object catalog.",
        "Correctness",
        RuleSeverity.Warning,
        false);

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Ast.Fragment is null ||
            context.ObjectCatalog is not { HasData: true, Scope.IsAuthoritative: true } catalog)
        {
            return [];
        }
        var visitor = new UnresolvedCallVisitor(catalog);
        context.Ast.Fragment.Accept(visitor);
        return visitor.Issues.Select(issue => new Diagnostic(
            ScriptDomHelpers.GetRange(issue.Fragment),
            $"Routine reference '{issue.DisplayName}' does not resolve in the authoritative object catalog.",
            Code: Metadata.RuleId,
            Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, false))).ToArray();
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class UnresolvedCallVisitor(IObjectCatalogProvider catalog) : TSqlFragmentVisitor
    {
        private readonly CatalogDependencyGraph _graph = CatalogDependencyGraph.For(catalog);
        internal List<UnresolvedCall> Issues { get; } = [];

        public override void ExplicitVisit(ExecuteStatement node)
        {
            if (node.ExecuteSpecification?.ExecutableEntity is ExecutableProcedureReference executable &&
                executable.ProcedureReference?.ProcedureVariable is null &&
                executable.ProcedureReference?.ProcedureReference?.Name is { ServerIdentifier: null } name)
            {
                Check(name.DatabaseIdentifier?.Value, name.SchemaIdentifier?.Value, name.BaseIdentifier?.Value,
                    CatalogObjectKindFilter.Procedure, name);
            }
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(FunctionCall node)
        {
            if (node.CallTarget is MultiPartIdentifierCallTarget target &&
                node.FunctionName?.Value is { Length: > 0 } name)
            {
                var identifiers = target.MultiPartIdentifier.Identifiers;
                if (identifiers.Count == 1)
                {
                    Check(null, identifiers[0].Value, name, CatalogObjectKindFilter.Function, node.FunctionName);
                }
                else if (identifiers.Count == 2)
                {
                    Check(identifiers[0].Value, identifiers[1].Value, name, CatalogObjectKindFilter.Function, node.FunctionName);
                }
            }
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SchemaObjectFunctionTableReference node)
        {
            if (node.SchemaObject is { ServerIdentifier: null } name)
            {
                Check(name.DatabaseIdentifier?.Value, name.SchemaIdentifier?.Value, name.BaseIdentifier?.Value,
                    CatalogObjectKindFilter.Function, name);
            }
            base.ExplicitVisit(node);
        }

        private void Check(
            string? database,
            string? schema,
            string? name,
            CatalogObjectKindFilter kind,
            TSqlFragment fragment)
        {
            if (string.IsNullOrWhiteSpace(name) || !IsInScope(database))
            {
                return;
            }
            if (kind == CatalogObjectKindFilter.Procedure &&
                SystemProcedureHelpers.IsSystemProcedureReference(name, schema))
            {
                return;
            }
            var normalizedSchema = schema ?? catalog.Scope.DefaultSchema;
            var matches = _graph.CountMatches(database, normalizedSchema, name, kind);
            if (matches == 0)
            {
                Issues.Add(new UnresolvedCall(fragment,
                    string.IsNullOrWhiteSpace(database)
                        ? $"{normalizedSchema}.{name}"
                        : $"{database}.{normalizedSchema}.{name}"));
            }
        }

        private bool IsInScope(string? database) =>
            database is null || catalog.Scope.Databases.Contains(database, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record UnresolvedCall(TSqlFragment Fragment, string DisplayName);
}

public sealed class UnreferencedObjectRule : IRule, IRuleOptionsDescriptorProvider
{
    public RuleMetadata Metadata { get; } = new(
        "unreferenced-object",
        "Detects cataloged SQL objects that have no incoming references.",
        "Correctness",
        RuleSeverity.Information,
        false);

    public IReadOnlyList<RuleOptionDescriptor> OptionDescriptors { get; } =
    [
        new("entrypoints", RuleOptionType.Text,
            "Comma-separated object names to exclude as application entry points.")
    ];

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ObjectCatalog is not { HasData: true } catalog)
        {
            return [];
        }
        var entrypoints = GetEntrypoints(context.Settings.Options);
        return catalog.GetAllObjects()
            .Where(obj => CatalogFilePathHelpers.SameFile(obj.DefinedInFile, context.FilePath))
            .Where(obj => !entrypoints.Contains(CatalogDependencyGraph.DisplayName(obj.Id)) &&
                          !entrypoints.Contains(obj.Id.Name))
            .Where(obj => !catalog.GetReferencesTo(obj.Id.DatabaseName, obj.Id.SchemaName, obj.Id.Name)
                .Any(reference => reference.Resolution == CatalogResolutionStatus.Resolved &&
                                  (reference.FromObject is null ||
                                   !CatalogDependencyGraph.IdentityEquals(reference.FromObject, obj.Id))))
            .Select(obj => new Diagnostic(
                obj.DefinedAt,
                $"{obj.Kind} '{CatalogDependencyGraph.DisplayName(obj.Id)}' has no incoming references in the object catalog.",
                Code: Metadata.RuleId,
                Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, false)))
            .ToArray();
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private static HashSet<string> GetEntrypoints(IRuleOptions? options)
    {
        if (options?.TryGetString("entrypoints", out var configured) is not true ||
            string.IsNullOrWhiteSpace(configured))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        return configured.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

}

public sealed class CircularObjectReferenceRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        "circular-object-reference",
        "Detects cycles between cataloged SQL objects.",
        "Correctness",
        RuleSeverity.Warning,
        false);

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ObjectCatalog is not { HasData: true } catalog)
        {
            return [];
        }
        var graph = CatalogDependencyGraph.For(catalog);
        return graph.Objects
            .Where(obj => CatalogFilePathHelpers.SameFile(obj.DefinedInFile, context.FilePath))
            .Select(obj => (Object: obj, Cycle: graph.FindCycle(obj)))
            .Where(item => item.Cycle is not null)
            .Select(item => new Diagnostic(
                item.Object.DefinedAt,
                $"Object reference cycle detected: {string.Join(" -> ", item.Cycle!.Select(node => CatalogDependencyGraph.DisplayName(node.Id)))}.",
                Code: Metadata.RuleId,
                Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, false)))
            .ToArray();
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

}
