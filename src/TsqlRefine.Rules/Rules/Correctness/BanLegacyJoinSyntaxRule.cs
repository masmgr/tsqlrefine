using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects legacy outer join syntax (*=, =*) which is deprecated and produces incorrect results.
/// </summary>
public sealed class BanLegacyJoinSyntaxRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "ban-legacy-join-syntax",
        Description: "Detects legacy outer join syntax (*=, =*) which is deprecated and produces incorrect results.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Scan tokens for *= or =* operators
        for (var i = 0; i < context.Tokens.Count; i++)
        {
            var token = context.Tokens[i];

            // Check for *= (left outer join)
            if (token.Text == "*" && i + 1 < context.Tokens.Count)
            {
                var nextToken = context.Tokens[i + 1];
                if (nextToken.Text == "=")
                {
                    var range = TokenHelpers.GetTokenRange(context.Tokens, i, i + 1);
                    yield return RuleHelpers.CreateDiagnostic(
                        range: range,
                        message: "Legacy outer join syntax '*=' is deprecated since SQL Server 2000. Use LEFT JOIN instead.",
                        code: "ban-legacy-join-syntax",
                        category: "Correctness",
                        fixable: false
                    );
                    i++; // Skip the '=' token
                }
            }
            // Check for =* (right outer join)
            else if (token.Text == "=" && i + 1 < context.Tokens.Count)
            {
                var nextToken = context.Tokens[i + 1];
                if (nextToken.Text == "*")
                {
                    var range = TokenHelpers.GetTokenRange(context.Tokens, i, i + 1);
                    yield return RuleHelpers.CreateDiagnostic(
                        range: range,
                        message: "Legacy outer join syntax '=*' is deprecated since SQL Server 2000. Use RIGHT JOIN instead.",
                        code: "ban-legacy-join-syntax",
                        category: "Correctness",
                        fixable: false
                    );
                    i++; // Skip the '*' token
                }
            }
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);
}
