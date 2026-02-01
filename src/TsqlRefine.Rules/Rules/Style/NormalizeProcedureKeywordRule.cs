using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// Rule that normalizes the abbreviated PROC keyword to its full form PROCEDURE.
/// </summary>
public sealed class NormalizeProcedureKeywordRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "normalize-procedure-keyword",
        Description: "Normalizes 'PROC' to 'PROCEDURE' for consistency.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: true
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tokens = context.Tokens;
        if (tokens is null || tokens.Count == 0)
        {
            yield break;
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (TokenHelpers.IsTrivia(token))
            {
                continue;
            }

            if (!TokenHelpers.IsKeyword(token, "PROC"))
            {
                continue;
            }

            var start = token.Start;
            var end = TokenHelpers.GetTokenEnd(token);

            yield return new Diagnostic(
                Range: new TsqlRefine.PluginSdk.Range(start, end),
                Message: "Use 'PROCEDURE' instead of 'PROC'.",
                Severity: null,
                Code: Metadata.RuleId,
                Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
            );
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostic);

        yield return new Fix(
            Title: "Use 'PROCEDURE'",
            Edits: new[] { new TextEdit(diagnostic.Range, "PROCEDURE") }
        );
    }
}
