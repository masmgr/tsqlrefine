using TsqlRefine.PluginSdk;

namespace CustomRule;

/// <summary>
/// Example custom rule that detects numeric literals (magic numbers) in SQL code.
/// This is for educational purposes to demonstrate how to create a custom rule plugin.
/// </summary>
public sealed class NoMagicNumbersRule : IRule
{
    /// <summary>
    /// Gets the metadata for this rule.
    /// </summary>
    public RuleMetadata Metadata { get; } = new(
        RuleId: "no-magic-numbers",
        Description: "Detects numeric literals (magic numbers) in SQL code. Consider using named constants or variables instead.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    /// <summary>
    /// Analyzes the SQL code and returns diagnostics for any numeric literals found.
    /// </summary>
    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Scan through tokens looking for numeric literals
        foreach (var token in context.Tokens)
        {
            // Check if this is a numeric literal token
            // Note: In a real implementation, you would check token.Type for numeric literal types
            // For this example, we'll check if the token text looks like a number
            if (IsNumericLiteral(token.Text))
            {
                // Skip common exceptions: 0, 1, 100 (percentages)
                if (IsCommonConstant(token.Text))
                {
                    continue;
                }

                yield return new Diagnostic(
                    Range: new TsqlRefine.PluginSdk.Range(
                        Start: token.Start,
                        End: new Position(token.Start.Line, token.Start.Character + token.Length)
                    ),
                    Message: $"Magic number '{token.Text}' found. Consider using a named constant or variable.",
                    Severity: DiagnosticSeverity.Information,
                    Code: Metadata.RuleId,
                    Data: new DiagnosticData(
                        RuleId: Metadata.RuleId,
                        Category: Metadata.Category,
                        Fixable: Metadata.Fixable
                    )
                );
            }
        }
    }

    /// <summary>
    /// Returns fixes for the diagnostic. This rule is not fixable, so returns an empty collection.
    /// </summary>
    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        // This rule doesn't provide automatic fixes
        yield break;
    }

    /// <summary>
    /// Checks if a token text represents a numeric literal.
    /// </summary>
    private static bool IsNumericLiteral(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // Simple heuristic: check if it starts with a digit or decimal point
        char firstChar = text.TrimStart('-', '+')[0];
        return char.IsDigit(firstChar) || firstChar == '.';
    }

    /// <summary>
    /// Checks if a number is a common constant that should be excluded (0, 1, 100).
    /// </summary>
    private static bool IsCommonConstant(string text)
    {
        return text == "0" || text == "1" || text == "100";
    }
}
