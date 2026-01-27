using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class KeywordCapitalizationRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "keyword-capitalization",
        Description: "SQL keywords should be in uppercase.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var token in context.Tokens)
        {
            if (!TokenHelpers.IsLikelyKeyword(token))
            {
                continue;
            }

            var text = token.Text;
            var upperText = text.ToUpperInvariant();

            if (text != upperText)
            {
                var start = token.Start;
                var end = TokenHelpers.GetTokenEnd(token);

                yield return new Diagnostic(
                    Range: new TsqlRefine.PluginSdk.Range(start, end),
                    Message: $"Keyword '{text}' should be uppercase ('{upperText}').",
                    Severity: null,
                    Code: Metadata.RuleId,
                    Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
                );
            }
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);
}
