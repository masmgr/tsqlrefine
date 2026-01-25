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
        if (string.IsNullOrEmpty(sql))
        {
            yield break;
        }

        var lines = sql.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
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
                    yield return new Diagnostic(
                        Range: new TsqlRefine.PluginSdk.Range(
                            new Position(i, 0),
                            new Position(i, line.Length)
                        ),
                        Message: "Consecutive GO batch separators found. Remove duplicate GO statements.",
                        Severity: null,
                        Code: Metadata.RuleId,
                        Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
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
