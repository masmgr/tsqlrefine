using System.Text.RegularExpressions;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules;

public sealed class AvoidSelectStarRule : IRule
{
    private static readonly Regex SelectStar = new(@"\bselect\s*\*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-select-star",
        Description: "Avoid SELECT * in queries.",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!SelectStar.IsMatch(context.Ast.RawSql))
        {
            yield break;
        }

        yield return new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 0)),
            Message: "Avoid SELECT *; explicitly list required columns.",
            Severity: null,
            Code: Metadata.RuleId,
            Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
        );
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostic);
        return Array.Empty<Fix>();
    }
}
