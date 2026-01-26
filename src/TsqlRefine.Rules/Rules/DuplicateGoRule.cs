using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class DuplicateGoRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "duplicate-go",
        Description: "Avoid consecutive GO batch separators.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sql = context.Ast.RawSql;
        var lines = TextAnalysisHelpers.SplitSqlLines(sql);
        if (lines.Length == 0)
        {
            yield break;
        }

        var lastGoLine = -1;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Check if this line is a GO statement (case-insensitive, standalone)
            if (line.Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                // Check if previous non-empty line was also GO
                if (lastGoLine >= 0)
                {
                    // Found consecutive GO statements
                    yield return TextAnalysisHelpers.CreateLineRangeDiagnostic(
                        lineNumber: i,
                        lineLength: line.Length,
                        message: "Consecutive GO batch separators found. Remove duplicate GO statements.",
                        code: Metadata.RuleId,
                        category: Metadata.Category,
                        fixable: Metadata.Fixable
                    );
                }

                lastGoLine = i;
            }
            else if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("--", StringComparison.Ordinal))
            {
                // Non-empty, non-comment line resets the GO counter
                // (but we allow empty lines and comments between GO statements)
                lastGoLine = -1;
            }
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);
}
