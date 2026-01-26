using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;
using System.Text.RegularExpressions;

namespace TsqlRefine.Rules.Rules;

public sealed partial class AvoidAmbiguousDatetimeLiteralRule : IRule
{
    [GeneratedRegex(@"^\s*\d{1,2}[/]\d{1,2}[/]\d{2,4}\s*$", RegexOptions.Compiled)]
    private static partial Regex SlashDatePattern();

    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-ambiguous-datetime-literal",
        Description: "Disallows slash-delimited date literals; they depend on language/locale and can silently change meaning - prefer ISO 8601.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        for (var i = 0; i < context.Tokens.Count; i++)
        {
            var token = context.Tokens[i];

            // Look for string literals that match date patterns with slashes
            var text = token.Text;

            // Check if this looks like a string literal (starts with quote)
            if (text.Length > 0 && (text[0] == '\'' || text[0] == '"'))
            {

                // Remove quotes if present
                if ((text.StartsWith('\'') && text.EndsWith('\'')) ||
                    (text.StartsWith('"') && text.EndsWith('"')))
                {
                    text = text[1..^1];
                }

                // Check for slash-delimited date pattern
                if (SlashDatePattern().IsMatch(text))
                {
                    var end = TokenHelpers.GetTokenEnd(token);
                    yield return new Diagnostic(
                        Range: new TsqlRefine.PluginSdk.Range(token.Start, end),
                        Message: $"Avoid slash-delimited date literal '{token.Text}'; it depends on locale settings. Use ISO 8601 format (YYYY-MM-DD) instead.",
                        Code: "avoid-ambiguous-datetime-literal",
                        Data: new DiagnosticData("avoid-ambiguous-datetime-literal", "Correctness", false)
                    );
                }
            }
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);
}
