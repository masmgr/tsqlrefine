using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

public sealed class AvoidAtatIdentityRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-atat-identity",
        Description: "Disallows @@IDENTITY; it can return values from triggers - prefer SCOPE_IDENTITY() or OUTPUT.",
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

            if (token.Text.Equals("@@IDENTITY", StringComparison.OrdinalIgnoreCase))
            {
                var end = TokenHelpers.GetTokenEnd(token);
                yield return new Diagnostic(
                    Range: new TsqlRefine.PluginSdk.Range(token.Start, end),
                    Message: "Avoid @@IDENTITY; it can return values from triggers. Use SCOPE_IDENTITY() or OUTPUT clause instead.",
                    Code: "avoid-atat-identity",
                    Data: new DiagnosticData("avoid-atat-identity", "Correctness", false)
                );
            }
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);
}
