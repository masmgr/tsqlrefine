namespace TsqlRefine.PluginSdk;

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
    int? MaxCompatLevel = null
);

public sealed record RuleSettings;

public sealed record Token(string Text, Position Start, int Length);

public sealed class ScriptDomAst
{
    public ScriptDomAst(string rawSql)
    {
        RawSql = rawSql ?? string.Empty;
    }

    public string RawSql { get; }
}

public sealed record RuleContext(
    string FilePath,
    int CompatLevel,
    ScriptDomAst Ast,
    IReadOnlyList<Token> Tokens,
    RuleSettings Settings
);

public interface IRule
{
    RuleMetadata Metadata { get; }

    IEnumerable<Diagnostic> Analyze(RuleContext context);

    IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic);
}

public interface IRuleProvider
{
    string Name { get; }

    int PluginApiVersion { get; }

    IReadOnlyList<IRule> GetRules();
}

