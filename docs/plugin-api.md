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
- `category` (categories from `docs/rules.md`)
- `defaultSeverity` (Error/Warning/Info/Hint)
- `fixable` (boolean)
- `minSqlVersion` / `maxSqlVersion` (optional)
- `documentationUri` (optional; defaults to built-in GitHub docs URL)

### 3.2 Execution

- Input: Parsed AST (including `GO`-separated batches), tokens, compatibility level, settings
- Output: `Diagnostic[]` (with Fix/suggestions if needed)

---

## 4. C# Interface Proposal (Assuming .NET)

> Even before the implementation language is finalized, you can define the expected interface shape upfront.

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
    RuleSettings Settings
);
```

*Note: `GetFixes` is only called when `Metadata.Fixable == true`.

---

## 5. Loading Specification

Plugins are specified in the configuration file.

Example:

```jsonc
{
  "plugins": [
    { "path": "./plugins/MyTeam.TsqlRefineRules.dll", "enabled": true }
  ]
}
```

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

- Core has a `pluginApiVersion` (e.g., `2`)
- Plugins declare `supportedApiVersions: [2]`
- If mismatched, the plugin is not loaded (reason displayed in `list-plugins`)
