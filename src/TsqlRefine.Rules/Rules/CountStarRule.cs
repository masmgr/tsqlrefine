using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class CountStarRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "count-star",
        Description: "Detects COUNT(*) usage and suggests using COUNT(1) or COUNT(column_name) for better clarity and consistency",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ranges = FindCountStarRanges(context.Tokens);
        foreach (var range in ranges)
        {
            yield return new Diagnostic(
                Range: range,
                Message: "Avoid COUNT(*). Use COUNT(1) for row counting or COUNT(column_name) to count non-null values for better clarity and consistency.",
                Severity: null,
                Code: Metadata.RuleId,
                Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
            );
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private static List<TsqlRefine.PluginSdk.Range> FindCountStarRanges(IReadOnlyList<Token> tokens)
    {
        var ranges = new List<TsqlRefine.PluginSdk.Range>();

        if (tokens is null || tokens.Count == 0)
        {
            return ranges;
        }

        for (var i = 0; i < tokens.Count - 2; i++)
        {
            // Look for COUNT (
            if (!TokenHelpers.IsKeyword(tokens[i], "count"))
            {
                continue;
            }

            // Skip trivia between COUNT and (
            var j = i + 1;
            while (j < tokens.Count && TokenHelpers.IsTrivia(tokens[j]))
            {
                j++;
            }

            if (j >= tokens.Count || tokens[j].Text != "(")
            {
                continue;
            }

            // Skip trivia after (
            j++;
            while (j < tokens.Count && TokenHelpers.IsTrivia(tokens[j]))
            {
                j++;
            }

            if (j >= tokens.Count || tokens[j].Text != "*")
            {
                continue;
            }

            // Found COUNT(*)
            var start = tokens[i].Start;
            var end = TokenHelpers.GetTokenEnd(tokens[j]);
            ranges.Add(new TsqlRefine.PluginSdk.Range(start, end));
        }

        return ranges;
    }
}
