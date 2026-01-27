using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class NestedBlockCommentsRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "nested-block-comments",
        Description: "Avoid nested block comments (/* /* */ */).",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var token in context.Tokens)
        {
            // Check if this is a multiline comment token
            if (token.TokenType?.Contains("Comment", StringComparison.Ordinal) == true &&
                token.Text.StartsWith("/*", StringComparison.Ordinal))
            {
                // Search for nested /* inside the comment
                var text = token.Text;
                var nestedStart = text.IndexOf("/*", 2, StringComparison.Ordinal);

                if (nestedStart > 0)
                {
                    // Found nested comment opening
                    var start = token.Start;
                    var end = TokenHelpers.GetTokenEnd(token);

                    yield return new Diagnostic(
                        Range: new TsqlRefine.PluginSdk.Range(start, end),
                        Message: "Nested block comments are not supported in T-SQL and may cause parsing errors.",
                        Severity: null,
                        Code: Metadata.RuleId,
                        Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
                    );
                }
            }
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);
}
