using Microsoft.SqlServer.TransactSql.ScriptDom;

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

public sealed record Token(string Text, Position Start, int Length, string? TokenType = null);

public sealed class ScriptDomAst
{
    public ScriptDomAst(string rawSql)
        : this(rawSql, null, Array.Empty<ParseError>(), Array.Empty<ParseError>(), null)
    {
    }

    public ScriptDomAst(
        string rawSql,
        TSqlFragment? fragment,
        IReadOnlyList<ParseError>? parseErrors,
        IReadOnlyList<ParseError>? tokenizationErrors,
        Exception? parserException = null)
    {
        RawSql = rawSql ?? string.Empty;
        Fragment = fragment;
        ParseErrors = parseErrors ?? Array.Empty<ParseError>();
        TokenizationErrors = tokenizationErrors ?? Array.Empty<ParseError>();
        ParserException = parserException;
    }

    public string RawSql { get; }

    public TSqlFragment? Fragment { get; }

    public IReadOnlyList<ParseError> ParseErrors { get; }

    public IReadOnlyList<ParseError> TokenizationErrors { get; }

    /// <summary>
    /// Gets the exception thrown by the parser, if any.
    /// This is set when the parser throws an unexpected exception during parsing.
    /// </summary>
    public Exception? ParserException { get; }
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
