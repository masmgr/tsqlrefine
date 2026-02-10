using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.PluginSdk;

/// <summary>
/// Severity level for a rule's default behavior.
/// </summary>
/// <remarks>
/// This enum is used in <see cref="RuleMetadata"/> to define a rule's default severity.
/// For diagnostic output, see <see cref="DiagnosticSeverity"/>.
/// </remarks>
public enum RuleSeverity
{
    /// <summary>A critical issue that should be addressed immediately.</summary>
    Error,
    /// <summary>An important issue that should be addressed but is not critical.</summary>
    Warning,
    /// <summary>An informational message.</summary>
    Information,
    /// <summary>A lightweight suggestion or hint.</summary>
    Hint
}

/// <summary>
/// Metadata describing a rule's identity and behavior.
/// </summary>
/// <param name="RuleId">Unique identifier for the rule (e.g., "avoid-select-star").</param>
/// <param name="Description">Human-readable description of what the rule checks.</param>
/// <param name="Category">Classification category (e.g., "Performance", "Security", "Style").</param>
/// <param name="DefaultSeverity">Default severity level when the rule reports an issue.</param>
/// <param name="Fixable">Whether the rule provides auto-fix capability via <see cref="IRule.GetFixes"/>.</param>
/// <param name="MinCompatLevel">Minimum SQL Server compatibility level (100-160) for this rule. Null means no minimum.</param>
/// <param name="MaxCompatLevel">Maximum SQL Server compatibility level (100-160) for this rule. Null means no maximum.</param>
public sealed record RuleMetadata(
    string RuleId,
    string Description,
    string Category,
    RuleSeverity DefaultSeverity,
    bool Fixable,
    int? MinCompatLevel = null,
    int? MaxCompatLevel = null
)
{
    private const string BaseDocUrl = "https://github.com/masmgr/tsqlrefine/blob/main/docs/Rules/";

    /// <summary>
    /// Gets the URI pointing to this rule's documentation page on GitHub.
    /// </summary>
    public Uri DocumentationUri { get; } = BuildDocumentationUri(RuleId, Category);

    private static Uri BuildDocumentationUri(string ruleId, string category)
    {
        var docFileName = ruleId.Replace('/', '-');
        var categoryDir = category.ToLowerInvariant();
        return new Uri($"{BaseDocUrl}{categoryDir}/{docFileName}.md");
    }
}

/// <summary>
/// Per-rule configuration settings. Currently empty, reserved for future use.
/// </summary>
/// <remarks>
/// Future versions may include:
/// <list type="bullet">
/// <item><description>Rule enablement/disablement</description></item>
/// <item><description>Severity overrides</description></item>
/// <item><description>Custom thresholds and parameters</description></item>
/// </list>
/// </remarks>
public sealed record RuleSettings;

/// <summary>
/// Represents a single token from the SQL source.
/// </summary>
/// <param name="Text">The token text (e.g., "SELECT", "FROM", "123").</param>
/// <param name="Start">The token's start position in the source file (0-based).</param>
/// <param name="Length">The token length in characters.</param>
/// <param name="TokenType">Optional token type classification. Common values include:
/// "Keyword", "Identifier", "Operator", "Literal", "Comment", "Whitespace".
/// May be null if token classification is not available.</param>
public sealed record Token(string Text, Position Start, int Length, string? TokenType = null);

/// <summary>
/// Wrapper around Microsoft ScriptDom's parsed T-SQL syntax tree.
/// Provides access to the AST, raw SQL, and any parsing errors.
/// </summary>
public sealed class ScriptDomAst
{
    /// <summary>
    /// Creates a new instance with only raw SQL (no parsed AST).
    /// </summary>
    /// <param name="rawSql">The original SQL text.</param>
    public ScriptDomAst(string rawSql)
        : this(rawSql, null, Array.Empty<ParseError>(), Array.Empty<ParseError>(), null)
    {
    }

    /// <summary>
    /// Creates a new instance with full parsing results.
    /// </summary>
    /// <param name="rawSql">The original SQL text.</param>
    /// <param name="fragment">The parsed syntax tree, or null if parsing failed.</param>
    /// <param name="parseErrors">Any syntax/semantic errors encountered during parsing.</param>
    /// <param name="tokenizationErrors">Any errors encountered during tokenization.</param>
    /// <param name="parserException">Any unexpected exception thrown by the parser.</param>
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

    /// <summary>
    /// Gets the original SQL text that was parsed.
    /// </summary>
    public string RawSql { get; }

    /// <summary>
    /// Gets the parsed syntax tree. May be null if parsing failed completely.
    /// </summary>
    public TSqlFragment? Fragment { get; }

    /// <summary>
    /// Gets any syntax or semantic errors encountered during parsing.
    /// </summary>
    public IReadOnlyList<ParseError> ParseErrors { get; }

    /// <summary>
    /// Gets any errors encountered during tokenization.
    /// </summary>
    public IReadOnlyList<ParseError> TokenizationErrors { get; }

    /// <summary>
    /// Gets the exception thrown by the parser, if any.
    /// This is set when the parser throws an unexpected exception during parsing.
    /// </summary>
    public Exception? ParserException { get; }

    /// <summary>
    /// Returns true if parsing encountered any errors or exceptions.
    /// </summary>
    public bool HasErrors =>
        ParseErrors.Count > 0 ||
        TokenizationErrors.Count > 0 ||
        ParserException != null;

    /// <summary>
    /// Returns true if the AST was successfully parsed without errors.
    /// </summary>
    public bool IsValid => Fragment != null && !HasErrors;
}

/// <summary>
/// Context provided to rules during analysis.
/// Contains all information needed to analyze SQL and report diagnostics.
/// </summary>
/// <param name="FilePath">Path to the SQL file being analyzed.</param>
/// <param name="CompatLevel">SQL Server compatibility level (100=2008, 110=2012, 120=2014, 150=2019, 160=2022).</param>
/// <param name="Ast">The parsed syntax tree and raw SQL.</param>
/// <param name="Tokens">Flat token stream for pattern matching.</param>
/// <param name="Settings">Per-rule configuration settings.</param>
public sealed record RuleContext(
    string FilePath,
    int CompatLevel,
    ScriptDomAst Ast,
    IReadOnlyList<Token> Tokens,
    RuleSettings Settings
);

/// <summary>
/// Interface that all rules must implement.
/// </summary>
/// <remarks>
/// Rules analyze SQL and report diagnostics. Optionally, they can provide auto-fixes.
/// <para>
/// Implementation patterns:
/// <list type="bullet">
/// <item><description>AST-based: Use <see cref="ScriptDomAst.Fragment"/> with TSqlFragmentVisitor for structural analysis.</description></item>
/// <item><description>Token-based: Use <see cref="RuleContext.Tokens"/> for fast pattern matching.</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IRule
{
    /// <summary>
    /// Gets the metadata describing this rule.
    /// </summary>
    RuleMetadata Metadata { get; }

    /// <summary>
    /// Analyzes SQL and returns any diagnostics found.
    /// </summary>
    /// <param name="context">The analysis context containing AST, tokens, and settings.</param>
    /// <returns>Zero or more diagnostics for issues found in the SQL.</returns>
    IEnumerable<Diagnostic> Analyze(RuleContext context);

    /// <summary>
    /// Returns possible fixes for a diagnostic.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>Zero or more fixes. Only called if <see cref="RuleMetadata.Fixable"/> is true.</returns>
    IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic);
}

/// <summary>
/// Interface for plugin assemblies to provide rules.
/// </summary>
/// <remarks>
/// Plugin assemblies should contain exactly one public class implementing this interface.
/// The host discovers and loads plugins by searching for implementations of this interface.
/// </remarks>
public interface IRuleProvider
{
    /// <summary>
    /// Gets the display name of this plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the Plugin API version this plugin was built against.
    /// Must match <see cref="PluginApi.CurrentVersion"/> for the plugin to load.
    /// </summary>
    int PluginApiVersion { get; }

    /// <summary>
    /// Gets all rules provided by this plugin.
    /// </summary>
    /// <returns>A read-only list of rule instances.</returns>
    IReadOnlyList<IRule> GetRules();
}
