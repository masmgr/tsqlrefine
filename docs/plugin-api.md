# Plugin API (Minimal Contract: Rule)

This project targets **Rules only** for pluginization (Formatter/Reporter are fixed in core).

Purpose:

- Allow adding team-specific conventions/dangerous patterns
- Manage compatibility between core updates and plugins

---

## 1. Basic Design

- 1 plugin = "collection of Rules"
- Load failures **do not crash core** (requirement: reliability)
- Compatibility is determined by "API version"; incompatible plugins are not loaded (warning is displayed)

---

## 2. Rule ID Naming

To avoid conflicts, plugins must use a namespace.

- Example: `myteam/avoid-select-star-in-procs`
- Core/standard plugins reserve `core/...` / `semantic/...`

---

## 3. Rule Contract (Concept)

The internal model of `tsqlrefine` depends on the implementation language, but the minimum required contract is as follows.

### 3.1 Metadata

- `ruleId` (unique)
- `description` / `messageTemplate`
- `category` (categories from `docs/Rules/README.md`)
- `defaultSeverity` (Error/Warning/Info/Hint)
- `fixable` (boolean)
- `minSqlVersion` / `maxSqlVersion` (optional)
- `documentationUri` (optional; defaults to built-in GitHub docs URL)

### 3.2 Execution

- Input: Parsed AST (including `GO`-separated batches), tokens, compatibility level, settings
- Output: `Diagnostic[]` (with Fix/suggestions if needed)

---

## 4. C# Interface

```csharp
public enum RuleSeverity
{
    Error,
    Warning,
    Information,
    Hint
}

public sealed record RuleMetadata(
    string RuleId,
    string Description,
    string Category,
    RuleSeverity DefaultSeverity,
    bool Fixable,
    int? MinCompatLevel = null,
    int? MaxCompatLevel = null,
    Uri? DocumentationUri = null  // custom docs URL; null if not available
);

public interface IRule
{
    RuleMetadata Metadata { get; }

    IEnumerable<Diagnostic> Analyze(RuleContext context);

    IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic);
}

public sealed record RuleContext(
    string FilePath,
    int CompatLevel,
    ScriptDomAst Ast,
    IReadOnlyList<Token> Tokens,
    RuleSettings Settings,
    ISchemaContext? SchemaContext = null,
    IObjectCatalogProvider? ObjectCatalog = null
);
```

### 4.1 Typed rule options

A plugin rule that accepts options implements `IRuleOptionsDescriptorProvider`. The engine validates
configuration before analysis and exposes values without leaking JSON types into the PluginSdk:

```csharp
public interface IRuleOptions
{
    bool TryGetBoolean(string name, out bool value);
    bool TryGetInt32(string name, out int value);
    bool TryGetString(string name, out string? value);
}

public interface IRuleOptionsDescriptorProvider
{
    IReadOnlyList<RuleOptionDescriptor> OptionDescriptors { get; }
}
```

Use `context.Settings.Options` to read a configured value and fall back to the rule's documented
default when the option is absent. Available descriptor types are `Flag`, `Number`, and `Text`.

*Note: `GetFixes` is only called when `Metadata.Fixable == true`.

### 4.2 Schema and object catalog access

`RuleContext.SchemaContext` and `RuleContext.ObjectCatalog` are `null` unless the user has
configured a `schema` snapshot or object catalog in `tsqlrefine.json`; rules must handle the
`null` case.

```csharp
public interface ISchemaProvider
{
    ResolvedTable? ResolveTable(string? database, string? schema, string name);
    ResolvedColumn? ResolveColumn(ResolvedTable table, string columnName);
    IReadOnlyList<SchemaColumnInfo> GetColumns(ResolvedTable table);
    string DefaultSchema { get; }
    SchemaSnapshotMetadata Metadata { get; }

    // ER relationship queries (primary keys, unique constraints, foreign keys, ...)
    SchemaPrimaryKeyInfo? GetPrimaryKey(ResolvedTable table);
    IReadOnlyList<SchemaUniqueConstraintInfo> GetUniqueConstraints(ResolvedTable table);
    IReadOnlyList<SchemaForeignKeyInfo> GetForeignKeys(ResolvedTable table);
}

public interface ISchemaContext : ISchemaProvider
{
    IRelationDeviationProvider? RelationDeviations { get; }
    bool HasRelationDeviations { get; }
}

public interface IRelationDeviationProvider
{
    bool HasData { get; }
    int TablePairCount { get; }
    // Deviation summary lookup for a canonicalized table pair.
}

public interface IObjectCatalogProvider
{
    bool HasData { get; }
    CatalogScopeInfo Scope { get; }
    CatalogObjectInfo? ResolveObject(string? database, string? schema, string name, CatalogObjectKindFilter kind);
    // Reference lookups for an object or one of its columns.
}
```

`RuleContext.Schema` and `RuleContext.RelationDeviations` are shorthand properties derived from
`SchemaContext`, kept for source compatibility with rules written before `ISchemaContext` was
introduced.

---

## 5. Loading Specification

> **Security warning:** Plugins are trusted code, not sandboxed extensions. A plugin DLL
> runs with the same operating-system permissions as `tsqlrefine` and can execute arbitrary
> code. `AssemblyLoadContext` isolates assembly loading but does not provide a security
> boundary. Load only DLLs you trust, and inspect a repository's discovered configuration
> before using `--allow-plugins`.

Plugins are specified in the configuration file.

Example:

```jsonc
{
  "plugins": [
    { "path": "./plugins/MyTeam.TsqlRefineRules.dll", "enabled": true }
  ]
}
```

### Path resolution

Plugin paths can be specified in two forms:

- **Relative path with directory** (e.g. `"plugins/MyPlugin.dll"`) — resolved relative to the config file directory (or CWD if no config file).
- **Filename only** (e.g. `"MyPlugin.dll"`) — searched in the following directories in order:
  1. Config file directory (or CWD)
  2. `CWD/.tsqlrefine/plugins/`
  3. `HOME/.tsqlrefine/plugins/`

The filename-only form is useful for sharing plugins across projects — place the DLL in `~/.tsqlrefine/plugins/` and reference it by name alone.

Resolved plugin files are accepted only when they remain inside one of these known search
directories. Absolute paths, UNC paths, and relative paths that escape their base directory
are rejected.

### Load process

At load time:

1. Load the assembly
2. Enumerate `IRule` implementations (or `IRuleProvider`)
3. Check for `ruleId` duplicates (duplicates are treated as errors, and that plugin is disabled)

Plugin rules are **enabled by default** regardless of which preset or ruleset is active. Users can disable individual plugin rules via the `rules` section in `tsqlrefine.json`:

```json
{
  "rules": {
    "myteam/noisy-rule": "none"
  }
}
```

---

## 6. Compatibility

- Core has a `pluginApiVersion` (currently `5`, see `PluginApi.CurrentVersion`)
- Plugin assemblies declare their built version via `[assembly: PluginApiVersion(5)]` (or the
  `IRuleProvider.PluginApiVersion` property)
- If mismatched, the plugin is not loaded (reason displayed in `list-plugins`)
