using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class DuplicateEmptyLineRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "duplicate-empty-line",
        Description: "Avoid consecutive empty lines (more than one blank line in a row).",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // We need to analyze the raw SQL text for consecutive newlines
        var sql = context.Ast.RawSql;
        if (string.IsNullOrEmpty(sql))
        {
            yield break;
        }

        var lines = sql.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var consecutiveEmptyCount = 0;
        var emptyLineStartIndex = -1;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                if (consecutiveEmptyCount == 0)
                {
                    emptyLineStartIndex = i;
                }
                consecutiveEmptyCount++;
            }
            else
            {
                if (consecutiveEmptyCount > 1)
                {
                    // Found duplicate empty lines
                    // Report on the second empty line (the duplicate)
                    var lineNumber = emptyLineStartIndex + 1;
                    yield return new Diagnostic(
                        Range: new TsqlRefine.PluginSdk.Range(
                            new Position(lineNumber, 0),
                            new Position(lineNumber, 0)
                        ),
                        Message: $"Found {consecutiveEmptyCount} consecutive empty lines. Use only one empty line for separation.",
                        Severity: null,
                        Code: Metadata.RuleId,
                        Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
                    );
                }

                consecutiveEmptyCount = 0;
                emptyLineStartIndex = -1;
            }
        }

        // Check if file ends with duplicate empty lines
        if (consecutiveEmptyCount > 1)
        {
            var lineNumber = emptyLineStartIndex + 1;
            yield return new Diagnostic(
                Range: new TsqlRefine.PluginSdk.Range(
                    new Position(lineNumber, 0),
                    new Position(lineNumber, 0)
                ),
                Message: $"Found {consecutiveEmptyCount} consecutive empty lines. Use only one empty line for separation.",
                Severity: null,
                Code: Metadata.RuleId,
                Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
            );
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);
}
